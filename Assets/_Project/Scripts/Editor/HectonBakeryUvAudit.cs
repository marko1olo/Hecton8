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
        private const float UvCellSize = 0.125f;
        private const float MaxUvEmptySpaceRatio = 0.30f;
        private const float UvAreaCoverageFudge = 1.18f;

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

        private readonly struct TriangleRecord
        {
            internal TriangleRecord(Vector2 a, Vector2 b, Vector2 c, int i0, int i1, int i2)
            {
                A = a;
                B = b;
                C = c;
                I0 = i0;
                I1 = i1;
                I2 = i2;
                MinX = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
                MinY = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
                MaxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                MaxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
            }

            internal Vector2 A { get; }
            internal Vector2 B { get; }
            internal Vector2 C { get; }
            internal int I0 { get; }
            internal int I1 { get; }
            internal int I2 { get; }
            internal float MinX { get; }
            internal float MinY { get; }
            internal float MaxX { get; }
            internal float MaxY { get; }
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

                List<Vector2> uv2 = new List<Vector2>(mesh.vertexCount);
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

                List<Vector2> uv0 = new List<Vector2>(mesh.vertexCount);
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
            Dictionary<long, List<TriangleRecord>> buckets = new Dictionary<long, List<TriangleRecord>>(256);
            List<int> indices = new List<int>((int)Math.Min(1024L, ResolveIndexCount(mesh)));

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
                    TriangleRecord triangle = new TriangleRecord(uv2[i0], uv2[i1], uv2[i2], i0, i1, i2);

                    if (Mathf.Abs(SignedArea(triangle.A, triangle.B, triangle.C)) <= DegenerateAreaThreshold)
                    {
                        reason = $"{mesh.name}: UV2 triangle is degenerate/self-intersecting.";
                        return true;
                    }

                    int minCellX = Mathf.FloorToInt(triangle.MinX / UvCellSize);
                    int minCellY = Mathf.FloorToInt(triangle.MinY / UvCellSize);
                    int maxCellX = Mathf.FloorToInt(triangle.MaxX / UvCellSize);
                    int maxCellY = Mathf.FloorToInt(triangle.MaxY / UvCellSize);

                    for (int cellY = minCellY; cellY <= maxCellY; cellY++)
                    {
                        for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                        {
                            long bucketKey = PackCell(cellX, cellY);
                            if (!buckets.TryGetValue(bucketKey, out List<TriangleRecord> bucket))
                                continue;

                            for (int bucketIndex = 0; bucketIndex < bucket.Count; bucketIndex++)
                            {
                                TriangleRecord candidate = bucket[bucketIndex];
                                if (SharesVertex(triangle, candidate))
                                    continue;

                                if (TrianglesOverlap(triangle, candidate))
                                {
                                    reason = $"{mesh.name}: UV2 overlap detected between non-adjacent lightmap triangles.";
                                    return true;
                                }
                            }
                        }
                    }

                    for (int cellY = minCellY; cellY <= maxCellY; cellY++)
                    {
                        for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                        {
                            long bucketKey = PackCell(cellX, cellY);
                            if (!buckets.TryGetValue(bucketKey, out List<TriangleRecord> bucket))
                            {
                                bucket = new List<TriangleRecord>(8);
                                buckets.Add(bucketKey, bucket);
                            }

                            bucket.Add(triangle);
                        }
                    }

                    testedTriangles++;
                }
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
            List<int> indices = new List<int>((int)Math.Min(1024L, ResolveIndexCount(mesh)));
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

        private static bool SharesVertex(TriangleRecord a, TriangleRecord b)
        {
            return a.I0 == b.I0 || a.I0 == b.I1 || a.I0 == b.I2
                || a.I1 == b.I0 || a.I1 == b.I1 || a.I1 == b.I2
                || a.I2 == b.I0 || a.I2 == b.I1 || a.I2 == b.I2;
        }

        private static bool TrianglesOverlap(TriangleRecord a, TriangleRecord b)
        {
            if (a.MaxX < b.MinX || a.MinX > b.MaxX || a.MaxY < b.MinY || a.MinY > b.MaxY)
                return false;

            if (ContainsPoint(a, b.A) || ContainsPoint(a, b.B) || ContainsPoint(a, b.C))
                return true;

            if (ContainsPoint(b, a.A) || ContainsPoint(b, a.B) || ContainsPoint(b, a.C))
                return true;

            return SegmentsIntersect(a.A, a.B, b.A, b.B)
                || SegmentsIntersect(a.A, a.B, b.B, b.C)
                || SegmentsIntersect(a.A, a.B, b.C, b.A)
                || SegmentsIntersect(a.B, a.C, b.A, b.B)
                || SegmentsIntersect(a.B, a.C, b.B, b.C)
                || SegmentsIntersect(a.B, a.C, b.C, b.A)
                || SegmentsIntersect(a.C, a.A, b.A, b.B)
                || SegmentsIntersect(a.C, a.A, b.B, b.C)
                || SegmentsIntersect(a.C, a.A, b.C, b.A);
        }

        private static bool ContainsPoint(TriangleRecord triangle, Vector2 point)
        {
            float area0 = SignedArea(triangle.A, triangle.B, point);
            float area1 = SignedArea(triangle.B, triangle.C, point);
            float area2 = SignedArea(triangle.C, triangle.A, point);
            bool hasNegative = area0 < -DegenerateAreaThreshold || area1 < -DegenerateAreaThreshold || area2 < -DegenerateAreaThreshold;
            bool hasPositive = area0 > DegenerateAreaThreshold || area1 > DegenerateAreaThreshold || area2 > DegenerateAreaThreshold;
            return !(hasNegative && hasPositive);
        }

        private static bool SegmentsIntersect(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
        {
            float o1 = SignedArea(a0, a1, b0);
            float o2 = SignedArea(a0, a1, b1);
            float o3 = SignedArea(b0, b1, a0);
            float o4 = SignedArea(b0, b1, a1);

            bool differentA = (o1 > DegenerateAreaThreshold && o2 < -DegenerateAreaThreshold) || (o1 < -DegenerateAreaThreshold && o2 > DegenerateAreaThreshold);
            bool differentB = (o3 > DegenerateAreaThreshold && o4 < -DegenerateAreaThreshold) || (o3 < -DegenerateAreaThreshold && o4 > DegenerateAreaThreshold);
            if (differentA && differentB)
                return true;

            return IsPointOnSegment(a0, a1, b0)
                || IsPointOnSegment(a0, a1, b1)
                || IsPointOnSegment(b0, b1, a0)
                || IsPointOnSegment(b0, b1, a1);
        }

        private static bool IsPointOnSegment(Vector2 a, Vector2 b, Vector2 point)
        {
            if (Mathf.Abs(SignedArea(a, b, point)) > DegenerateAreaThreshold)
                return false;

            return point.x >= Mathf.Min(a.x, b.x) - DegenerateAreaThreshold
                && point.x <= Mathf.Max(a.x, b.x) + DegenerateAreaThreshold
                && point.y >= Mathf.Min(a.y, b.y) - DegenerateAreaThreshold
                && point.y <= Mathf.Max(a.y, b.y) + DegenerateAreaThreshold;
        }

        private static float SignedArea(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        private static long PackCell(int x, int y)
        {
            return ((long)x << 32) ^ (uint)y;
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
