#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hecton8.Editor;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
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
        private const int UvOverlapRasterResolution = 256;
        private const int UvOverlapRasterPixelCount = UvOverlapRasterResolution * UvOverlapRasterResolution;
        private const int UvOverlapRasterWordCount = UvOverlapRasterPixelCount / 32;
        private const float UvRasterInsideEpsilon = 0.0000001f;
        private const int UvScratchInitialCapacity = 8192;
        private const int IndexScratchInitialCapacity = 12288;

        // COLD ALLOC: editor audit scratch buffers reused across the single-threaded UV pass.
        private static readonly List<Vector2> s_Uv2Scratch = new List<Vector2>(UvScratchInitialCapacity);
        private static readonly List<Vector2> s_Uv0Scratch = new List<Vector2>(UvScratchInitialCapacity);
        private static readonly List<int> s_IndexScratch = new List<int>(IndexScratchInitialCapacity);
        private static readonly List<MeshFilter> s_MeshFilterScratch = new List<MeshFilter>(32);
        private static readonly List<SkinnedMeshRenderer> s_SkinnedRendererScratch = new List<SkinnedMeshRenderer>(16);
        // COLD ALLOC: uint[2048] - 256x256 UV overlap raster bitset - owner: HectonBakeryUvAudit
        private static readonly uint[] s_UvOverlapRasterBits = new uint[UvOverlapRasterWordCount];

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

                    // An offline forge package whose manifest explicitly declares generateSecondaryUV=false owns
                    // its own UV1. Unity's auto-unwrap OVERWRITES that channel, and 3dmodel.md section 3 makes
                    // TexCoord1 authored data: "Lightmap, detail, atlas remap, or packed baked masks when
                    // required." The finding is still recorded rather than dropped, because a real UV1 defect in
                    // a generated package must be fixed in the generator, not hidden behind an importer toggle.
                    if (HectonFBXPostprocessor.TryResolveForgeImportContract(
                            modelPath,
                            out HectonFBXPostprocessor.ForgeImportContract forgeContract) &&
                        forgeContract.SuppressSecondaryUv)
                    {
                        result.ManualReviewModels.Add(
                            $"{modelPath}: offline forge package (manifest '{forgeContract.ManifestPath}') declares " +
                            "generateSecondaryUV=false, so importer auto-unwrap is SKIPPED - it would overwrite the " +
                            "authored TexCoord1 that 3dmodel.md section 3 requires. Fix the generator's UV1 layout " +
                            $"instead. Reported issues: {string.Join(" | ", inspection.Issues)}");
                        continue;
                    }

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

        internal static bool TryValidateImportedModelUv2(GameObject importedRoot, string modelPath, out string reason)
        {
            reason = string.Empty;
            if (importedRoot == null)
                return false;

            s_MeshFilterScratch.Clear();
            importedRoot.GetComponentsInChildren(true, s_MeshFilterScratch);
            for (int i = 0; i < s_MeshFilterScratch.Count; i++)
            {
                Mesh mesh = s_MeshFilterScratch[i] != null ? s_MeshFilterScratch[i].sharedMesh : null;
                if (TryValidateMeshUv2(mesh, modelPath, out reason))
                    return true;
            }

            s_SkinnedRendererScratch.Clear();
            importedRoot.GetComponentsInChildren(true, s_SkinnedRendererScratch);
            for (int i = 0; i < s_SkinnedRendererScratch.Count; i++)
            {
                Mesh mesh = s_SkinnedRendererScratch[i] != null ? s_SkinnedRendererScratch[i].sharedMesh : null;
                if (TryValidateMeshUv2(mesh, modelPath, out reason))
                    return true;
            }

            return false;
        }

        private static bool TryValidateMeshUv2(Mesh mesh, string modelPath, out string reason)
        {
            reason = string.Empty;
            if (mesh == null || mesh.vertexCount <= 0)
                return false;

            List<Vector2> uv2 = s_Uv2Scratch;
            EnsureScratchCapacity(uv2, mesh.vertexCount);
            uv2.Clear();
            mesh.GetUVs(1, uv2);
            if (uv2.Count != mesh.vertexCount)
                return false;

            if (!TryDetectUvOverlap(mesh, uv2, out string overlapReason))
                return false;

            reason = modelPath + ": " + overlapReason;
            return true;
        }

        private static bool TryDetectUvOverlap(Mesh mesh, List<Vector2> uv2, out string reason)
        {
            int testedTriangles = 0;
            List<int> indices = s_IndexScratch;
            EnsureScratchCapacity(indices, ResolveIndexScratchCapacity(mesh));
            ClearUvOverlapRaster();

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
                    Vector2 a = uv2[i0];
                    Vector2 b = uv2[i1];
                    Vector2 c = uv2[i2];
                    float signedArea = SignedArea(a, b, c);
                    float triangleArea = Mathf.Abs(signedArea) * 0.5f;

                    if (triangleArea <= DegenerateAreaThreshold)
                    {
                        reason = $"{mesh.name}: UV2 triangle is degenerate/self-intersecting.";
                        return true;
                    }

                    if (TryRasterizeUvTriangle(a, b, c, signedArea, out int overlapCell))
                    {
                        int overlapX = overlapCell & (UvOverlapRasterResolution - 1);
                        int overlapY = overlapCell >> 8;
                        reason = $"{mesh.name}: Overlapped UVs detected by {UvOverlapRasterResolution}x{UvOverlapRasterResolution} raster at cell {overlapX},{overlapY}.";
                        return true;
                    }

                    testedTriangles++;
                }
            }

            reason = string.Empty;
            return false;
        }

        private static void ClearUvOverlapRaster()
        {
            for (int i = 0; i < s_UvOverlapRasterBits.Length; i++)
                s_UvOverlapRasterBits[i] = 0u;
        }

        private static bool TryRasterizeUvTriangle(
            Vector2 a,
            Vector2 b,
            Vector2 c,
            float signedArea,
            out int overlapCell)
        {
            overlapCell = -1;

            float minU = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
            float maxU = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
            float minV = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
            float maxV = Mathf.Max(a.y, Mathf.Max(b.y, c.y));

            int minX = Mathf.Clamp((int)(minU * UvOverlapRasterResolution), 0, UvOverlapRasterResolution - 1);
            int maxX = Mathf.Clamp((int)(maxU * UvOverlapRasterResolution), 0, UvOverlapRasterResolution - 1);
            int minY = Mathf.Clamp((int)(minV * UvOverlapRasterResolution), 0, UvOverlapRasterResolution - 1);
            int maxY = Mathf.Clamp((int)(maxV * UvOverlapRasterResolution), 0, UvOverlapRasterResolution - 1);
            float invResolution = 1f / UvOverlapRasterResolution;

            for (int y = minY; y <= maxY; y++)
            {
                float sampleV = (y + 0.5f) * invResolution;
                for (int x = minX; x <= maxX; x++)
                {
                    float sampleU = (x + 0.5f) * invResolution;
                    if (!IsPointInsideTriangleStrict(sampleU, sampleV, a, b, c, signedArea))
                        continue;

                    int cell = (y * UvOverlapRasterResolution) + x;
                    if (!TryMarkUvRasterCell(cell))
                    {
                        overlapCell = cell;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsPointInsideTriangleStrict(
            float u,
            float v,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            float signedArea)
        {
            Vector2 p = new Vector2(u, v);
            float edge0 = SignedArea(b, c, p);
            float edge1 = SignedArea(c, a, p);
            float edge2 = SignedArea(a, b, p);

            if (signedArea > 0f)
            {
                return edge0 > UvRasterInsideEpsilon &&
                       edge1 > UvRasterInsideEpsilon &&
                       edge2 > UvRasterInsideEpsilon;
            }

            return edge0 < -UvRasterInsideEpsilon &&
                   edge1 < -UvRasterInsideEpsilon &&
                   edge2 < -UvRasterInsideEpsilon;
        }

        private static bool TryMarkUvRasterCell(int cell)
        {
            int word = cell >> 5;
            uint mask = 1u << (cell & 31);
            uint previous = s_UvOverlapRasterBits[word];
            if ((previous & mask) != 0u)
                return false;

            s_UvOverlapRasterBits[word] = previous | mask;
            return true;
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
