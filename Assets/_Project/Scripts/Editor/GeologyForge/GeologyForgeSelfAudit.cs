using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.GeologyForge
{
    internal static unsafe class GeologyForgeSelfAudit
    {
        [MenuItem("Hecton8/Geology Forge/Run Layout Self Audit", false, 182)]
        public static void RunAndWriteReport()
        {
            GeologyVertexLayoutValidator.ValidateStruct();
            int meshCount = 0;
            int meshFailures = 0;
            int unmanifestedMeshCount = 0;
            int collisionMeshCount = 0;
            int collisionMeshFailures = 0;
            int prefabCount = 0;
            int prefabFailures = 0;
            StringBuilder failures = new StringBuilder(1024);
            HashSet<string> manifestMeshPaths = new HashSet<string>(256, StringComparer.Ordinal);
            bool manifestValid = TryValidateManifest(manifestMeshPaths, out uint manifestRecords, out long manifestBytes, out string manifestReason);
            ValidateGeneratedMeshes(manifestMeshPaths, ref meshCount, ref meshFailures, ref unmanifestedMeshCount, ref collisionMeshCount, ref collisionMeshFailures, failures);
            ValidateGeneratedPrefabs(ref prefabCount, ref prefabFailures, failures);
            bool noOutput = (meshCount - collisionMeshCount) == 0 || manifestRecords == 0u;
            bool manifestFailure = !manifestValid || manifestRecords == 0u;
            WriteReport(meshCount, meshFailures, unmanifestedMeshCount, collisionMeshCount, collisionMeshFailures, prefabCount, prefabFailures, manifestMeshPaths.Count, manifestValid, manifestRecords, manifestBytes, manifestReason, manifestFailure, noOutput, failures);
            Debug.Log($"[1606] Geology layout audit wrote {GeologyForgeConstants.LayoutAuditReportPath} meshes={meshCount} meshFailures={meshFailures} collisionMeshes={collisionMeshCount} prefabFailures={prefabFailures} manifestValid={manifestValid} noOutput={noOutput}.");
        }

        private static void ValidateGeneratedMeshes(
            HashSet<string> manifestMeshPaths,
            ref int meshCount,
            ref int meshFailures,
            ref int unmanifestedMeshCount,
            ref int collisionMeshCount,
            ref int collisionMeshFailures,
            StringBuilder failures)
        {
            if (!Directory.Exists(GeologyForgeConstants.MeshOutputFolder))
                return;

            string[] files = Directory.GetFiles(GeologyForgeConstants.MeshOutputFolder, "*.asset", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                {
                    meshFailures++;
                    AppendFailure(failures, path, "NON_MESH_ASSET_IN_GEOLOGY_OUTPUT_FOLDER");
                    continue;
                }

                meshCount++;
                if (IsCollisionProxyPath(path))
                {
                    collisionMeshCount++;
                    if (!ValidateCollisionProxyMesh(path, mesh, failures))
                    {
                        meshFailures++;
                        collisionMeshFailures++;
                    }

                    continue;
                }

                if (!manifestMeshPaths.Contains(path))
                {
                    meshFailures++;
                    unmanifestedMeshCount++;
                    AppendFailure(failures, path, "UNMANIFESTED_MESH_ASSET");
                }

                try
                {
                    GeologyVertexLayoutValidator.ValidateMesh(mesh);
                }
                catch (Exception ex)
                {
                    meshFailures++;
                    AppendFailure(failures, path, ex.Message);
                }
            }
        }

        private static bool ValidateCollisionProxyMesh(string path, Mesh mesh, StringBuilder failures)
        {
            bool valid = true;
            if (mesh == null)
            {
                AppendFailure(failures, path, "COL_PROXY_MISSING_MESH");
                return false;
            }

            if (!mesh.name.StartsWith("COL_", StringComparison.Ordinal))
            {
                valid = false;
                AppendFailure(failures, path, "COL_PROXY_BAD_NAME");
            }

            long triangleCount = mesh.subMeshCount > 0 ? (long)mesh.GetIndexCount(0) / 3L : 0L;
            if (triangleCount <= 0L || triangleCount > GeologyForgeConstants.CollisionTriangleBudget)
            {
                valid = false;
                AppendFailure(failures, path, "COL_PROXY_TRIANGLE_BUDGET");
            }

            Bounds bounds = mesh.bounds;
            if (!math.all(math.isfinite(new float3(bounds.center.x, bounds.center.y, bounds.center.z))) ||
                !math.all(math.isfinite(new float3(bounds.extents.x, bounds.extents.y, bounds.extents.z))) ||
                bounds.extents.x <= 0f ||
                bounds.extents.y <= 0f ||
                bounds.extents.z <= 0f)
            {
                valid = false;
                AppendFailure(failures, path, "COL_PROXY_BAD_BOUNDS");
            }

            return valid;
        }

        private static void ValidateGeneratedPrefabs(ref int prefabCount, ref int prefabFailures, StringBuilder failures)
        {
            if (!Directory.Exists(GeologyForgeConstants.PrefabOutputFolder))
                return;

            string[] files = Directory.GetFiles(GeologyForgeConstants.PrefabOutputFolder, "*.prefab", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                prefabCount++;
                if (prefab == null)
                {
                    prefabFailures++;
                    AppendFailure(failures, path, "PREFAB_LOAD_FAILED");
                    continue;
                }

                if (!ValidateGeneratedPrefab(path, prefab, failures))
                    prefabFailures++;
            }
        }

        private static bool ValidateGeneratedPrefab(string path, GameObject prefab, StringBuilder failures)
        {
            bool valid = true;
            Transform colliderRoot = prefab.transform.Find("COL_ConvexProxy_1716");
            MeshCollider collider = colliderRoot != null ? colliderRoot.GetComponent<MeshCollider>() : null;
            if (collider == null || collider.sharedMesh == null)
            {
                AppendFailure(failures, path, "PREFAB_MISSING_COL_CONVEX_PROXY_1716");
                return false;
            }

            if (!collider.sharedMesh.name.StartsWith("COL_", StringComparison.Ordinal))
            {
                valid = false;
                AppendFailure(failures, path, "PREFAB_COLLIDER_NOT_COL_PROXY");
            }

            if (!collider.convex)
            {
                valid = false;
                AppendFailure(failures, path, "PREFAB_COLLIDER_NOT_CONVEX");
            }

            if ((collider.cookingOptions & MeshColliderCookingOptions.CookForFasterSimulation) == 0 ||
                (collider.cookingOptions & MeshColliderCookingOptions.EnableMeshCleaning) == 0 ||
                (collider.cookingOptions & MeshColliderCookingOptions.WeldColocatedVertices) == 0)
            {
                valid = false;
                AppendFailure(failures, path, "PREFAB_COLLIDER_BAD_COOKING_OPTIONS");
            }

            long colliderTriangles = collider.sharedMesh.subMeshCount > 0 ? (long)collider.sharedMesh.GetIndexCount(0) / 3L : 0L;
            if (colliderTriangles <= 0L || colliderTriangles > GeologyForgeConstants.CollisionTriangleBudget)
            {
                valid = false;
                AppendFailure(failures, path, "PREFAB_COLLIDER_TRIANGLE_BUDGET");
            }

            LODGroup lodGroup = prefab.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                valid = false;
                AppendFailure(failures, path, "PREFAB_MISSING_LODGROUP");
            }

            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            bool hasSeparateVisualMesh = false;
            bool hasVisualBounds = false;
            Bounds visualBounds = default;
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh visualMesh = filters[i].sharedMesh;
                if (visualMesh != null && visualMesh != collider.sharedMesh)
                {
                    hasSeparateVisualMesh = true;
                    if (TryEncapsulateVisualMeshBounds(prefab.transform, filters[i].transform, visualMesh.bounds, ref visualBounds, ref hasVisualBounds))
                        continue;

                    valid = false;
                    AppendFailure(failures, path, "PREFAB_VISUAL_BOUNDS_INVALID");
                }
            }

            if (!hasSeparateVisualMesh)
            {
                valid = false;
                AppendFailure(failures, path, "PREFAB_MISSING_SEPARATE_VISUAL_MESH");
            }

            if (hasVisualBounds && !BoundsContains(collider.sharedMesh.bounds, visualBounds, 0.001f))
            {
                valid = false;
                AppendFailure(failures, path, "PREFAB_COLLIDER_BOUNDS_UNDER_VISUAL");
            }

            if (hasVisualBounds && !ValidateOccluderStaticGate(path, filters, collider.sharedMesh, visualBounds, failures))
                valid = false;

            return valid;
        }

        private static bool ValidateOccluderStaticGate(string path, MeshFilter[] filters, Mesh colliderMesh, Bounds visualBounds, StringBuilder failures)
        {
            float volume = CalculateBoundsVolume(visualBounds);
            bool mayOcclude = volume >= GeologyForgeConstants.OccluderStaticMinimumVolumeCubicMeters;
            bool valid = true;
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.sharedMesh == null)
                    continue;
                if (filter.sharedMesh == colliderMesh)
                    continue;

                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(filter.gameObject);
                bool isOccluder = (flags & StaticEditorFlags.OccluderStatic) != 0;
                bool isOccludee = (flags & StaticEditorFlags.OccludeeStatic) != 0;
                bool isBatchingStatic = (flags & StaticEditorFlags.BatchingStatic) != 0;
                if (!isOccludee || !isBatchingStatic)
                {
                    valid = false;
                    AppendFailure(failures, path, "PREFAB_RENDERER_STATIC_FLAGS_MISSING");
                }

                if (!mayOcclude && isOccluder)
                {
                    valid = false;
                    AppendFailure(failures, path, "PREFAB_RENDERER_OCCLUDER_TOO_SMALL");
                }

                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    valid = false;
                    AppendFailure(failures, path, "PREFAB_RENDERER_MISSING");
                    continue;
                }

                if (!ValidateStaticRockRenderer(path, renderer, failures))
                    valid = false;
            }

            return valid;
        }

        private static bool ValidateStaticRockRenderer(string path, MeshRenderer renderer, StringBuilder failures)
        {
            bool valid = true;
            if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.On)
            {
                valid = false;
                AppendFailure(failures, path, "PREFAB_RENDERER_SHADOW_CASTING");
            }

            if (!renderer.receiveShadows)
            {
                valid = false;
                AppendFailure(failures, path, "PREFAB_RENDERER_RECEIVE_SHADOWS");
            }

            if (renderer.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
            {
                valid = false;
                AppendFailure(failures, path, "PREFAB_RENDERER_MOTION_VECTOR");
            }

            if (renderer.lightProbeUsage != UnityEngine.Rendering.LightProbeUsage.BlendProbes ||
                renderer.reflectionProbeUsage != UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes)
            {
                valid = false;
                AppendFailure(failures, path, "PREFAB_RENDERER_PROBE_USAGE");
            }

            return valid;
        }

        private static bool TryEncapsulateVisualMeshBounds(
            Transform root,
            Transform meshTransform,
            Bounds meshBounds,
            ref Bounds combinedBounds,
            ref bool hasBounds)
        {
            if (!IsFiniteBounds(meshBounds))
                return false;

            Matrix4x4 localToRoot = CalculateLocalToRootMatrix(root, meshTransform);
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;
            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 corner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 transformed = localToRoot.MultiplyPoint3x4(corner);
                        if (!math.all(math.isfinite(new float3(transformed.x, transformed.y, transformed.z))))
                            return false;

                        if (!hasBounds)
                        {
                            combinedBounds = new Bounds(transformed, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            combinedBounds.Encapsulate(transformed);
                        }
                    }
                }
            }

            return true;
        }

        private static Matrix4x4 CalculateLocalToRootMatrix(Transform root, Transform node)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            Transform current = node;
            while (current != null && current != root)
            {
                matrix = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * matrix;
                current = current.parent;
            }

            return matrix;
        }

        private static bool BoundsContains(Bounds container, Bounds content, float epsilon)
        {
            if (!IsFiniteBounds(container) || !IsFiniteBounds(content))
                return false;

            Vector3 containerMin = container.min;
            Vector3 containerMax = container.max;
            Vector3 contentMin = content.min;
            Vector3 contentMax = content.max;
            return containerMin.x <= contentMin.x + epsilon &&
                   containerMin.y <= contentMin.y + epsilon &&
                   containerMin.z <= contentMin.z + epsilon &&
                   containerMax.x >= contentMax.x - epsilon &&
                   containerMax.y >= contentMax.y - epsilon &&
                   containerMax.z >= contentMax.z - epsilon;
        }

        private static bool IsFiniteBounds(Bounds bounds)
        {
            return math.all(math.isfinite(new float3(bounds.center.x, bounds.center.y, bounds.center.z))) &&
                   math.all(math.isfinite(new float3(bounds.extents.x, bounds.extents.y, bounds.extents.z))) &&
                   bounds.extents.x >= 0f &&
                   bounds.extents.y >= 0f &&
                   bounds.extents.z >= 0f;
        }

        private static float CalculateBoundsVolume(Bounds bounds)
        {
            Vector3 size = bounds.size;
            if (!math.all(math.isfinite(new float3(size.x, size.y, size.z))))
                return 0f;
            return Mathf.Max(0f, size.x) * Mathf.Max(0f, size.y) * Mathf.Max(0f, size.z);
        }

        private static bool TryValidateManifest(HashSet<string> manifestMeshPaths, out uint recordCount, out long byteLength, out string reason)
        {
            recordCount = 0u;
            byteLength = 0L;
            reason = "MISSING";
            if (!File.Exists(GeologyForgeConstants.ManifestPath))
                return false;

            if (!BitConverter.IsLittleEndian)
            {
                reason = "BIG_ENDIAN_HOST_UNSUPPORTED";
                return false;
            }

            using (FileStream stream = new FileStream(GeologyForgeConstants.ManifestPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byteLength = stream.Length;
                int headerSize = UnsafeUtility.SizeOf<GeologyMeshManifestHeader>();
                byte* headerPtr = stackalloc byte[headerSize];
                Span<byte> headerBytes = new Span<byte>(headerPtr, headerSize);
                if (!ReadExact(stream, headerBytes))
                {
                    reason = "SHORT_HEADER";
                    return false;
                }

                GeologyMeshManifestHeader header = UnsafeUtility.ReadArrayElement<GeologyMeshManifestHeader>(headerPtr, 0);
                recordCount = header.RecordCount;
                if (header.Magic != GeologyForgeConstants.ManifestMagic)
                {
                    reason = "BAD_MAGIC";
                    return false;
                }

                if (header.Version != GeologyForgeConstants.ManifestVersion)
                {
                    reason = "BAD_VERSION";
                    return false;
                }

                if (header.HeaderSize != (uint)headerSize || header.RecordSize != (uint)UnsafeUtility.SizeOf<GeologyMeshManifestRecord>())
                {
                    reason = "BAD_RECORD_LAYOUT";
                    return false;
                }

                if (header.VertexStrideBytes != GeologyForgeConstants.VertexStrideBytes || header.LodCount != (uint)GeologyForgeConstants.LodCount)
                {
                    reason = "BAD_VERTEX_OR_LOD_CONTRACT";
                    return false;
                }

                long expectedBytes = header.HeaderSize + ((long)header.RecordCount * header.RecordSize);
                if (byteLength != expectedBytes)
                {
                    reason = "BAD_FILE_LENGTH";
                    return false;
                }

                int recordSize = UnsafeUtility.SizeOf<GeologyMeshManifestRecord>();
                byte* recordPtr = stackalloc byte[recordSize];
                Span<byte> recordBytes = new Span<byte>(recordPtr, recordSize);
                for (uint i = 0; i < header.RecordCount; i++)
                {
                    if (!ReadExact(stream, recordBytes))
                    {
                        reason = "SHORT_RECORD";
                        return false;
                    }

                    GeologyMeshManifestRecord record = UnsafeUtility.ReadArrayElement<GeologyMeshManifestRecord>(recordPtr, 0);
                    if (!ValidateManifestRecord(record, manifestMeshPaths))
                    {
                        reason = "BAD_RECORD";
                        return false;
                    }
                }

                if (manifestMeshPaths.Count != header.RecordCount * GeologyForgeConstants.LodCount)
                {
                    reason = "BAD_GUID_REFERENCE_COUNT";
                    return false;
                }

                if (stream.Length != byteLength)
                {
                    reason = "UNSTABLE_FILE_LENGTH";
                    return false;
                }
            }

            reason = "OK";
            return true;
        }

        private static bool ValidateManifestRecord(GeologyMeshManifestRecord record, HashSet<string> manifestMeshPaths)
        {
            if (record.VertexStrideBytes != GeologyForgeConstants.VertexStrideBytes)
                return false;
            if ((record.Flags & GeologyForgeConstants.ManifestFlagBrgReady) == 0u)
                return false;
            if (record.Lod0Triangles <= 0 || record.Lod1Triangles <= 0 || record.Lod2Triangles <= 0)
                return false;
            if (!math.all(math.isfinite(record.SectorAup)))
                return false;
            if (!math.all(math.isfinite(record.BoundsCenter)) || !math.all(math.isfinite(record.BoundsExtents)))
                return false;
            if (!math.all(record.BoundsExtents > 0f))
                return false;
            if (record.Lod0GuidHigh == 0UL && record.Lod0GuidLow == 0UL)
                return false;
            if (record.Lod1GuidHigh == 0UL && record.Lod1GuidLow == 0UL)
                return false;
            if (record.Lod2GuidHigh == 0UL && record.Lod2GuidLow == 0UL)
                return false;
            if (!ValidateMeshGuid(record.Lod0GuidHigh, record.Lod0GuidLow, manifestMeshPaths))
                return false;
            if (!ValidateMeshGuid(record.Lod1GuidHigh, record.Lod1GuidLow, manifestMeshPaths))
                return false;
            if (!ValidateMeshGuid(record.Lod2GuidHigh, record.Lod2GuidLow, manifestMeshPaths))
                return false;
            return true;
        }

        private static bool ValidateMeshGuid(ulong high, ulong low, HashSet<string> manifestMeshPaths)
        {
            if (high == 0UL && low == 0UL)
                return false;

            string path = AssetDatabase.GUIDToAssetPath(high.ToString("x16") + low.ToString("x16"));
            if (string.IsNullOrEmpty(path))
                return false;
            path = path.Replace('\\', '/');
            if (!IsMeshOutputPath(path))
                return false;
            if (!manifestMeshPaths.Add(path))
                return false;

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
                return false;

            try
            {
                GeologyVertexLayoutValidator.ValidateMesh(mesh);
            }
            catch
            {
                return false;
            }

            return true;
        }

        private static bool IsMeshOutputPath(string path)
        {
            return path.StartsWith(GeologyForgeConstants.MeshOutputFolder + "/", StringComparison.Ordinal);
        }

        private static bool IsCollisionProxyPath(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            return !string.IsNullOrEmpty(fileName) && fileName.StartsWith("COL_GEN_Geology_", StringComparison.Ordinal);
        }

        private static bool ReadExact(FileStream stream, Span<byte> target)
        {
            int read = 0;
            while (read < target.Length)
            {
                int chunk = stream.Read(target.Slice(read));
                if (chunk <= 0)
                    return false;
                read += chunk;
            }

            return true;
        }

        private static void WriteReport(
            int meshCount,
            int meshFailures,
            int unmanifestedMeshCount,
            int collisionMeshCount,
            int collisionMeshFailures,
            int prefabCount,
            int prefabFailures,
            int manifestMeshReferenceCount,
            bool manifestValid,
            uint manifestRecords,
            long manifestBytes,
            string manifestReason,
            bool manifestFailure,
            bool noOutput,
            StringBuilder failures)
        {
            string folder = Path.GetDirectoryName(GeologyForgeConstants.LayoutAuditReportPath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            int visualMeshCount = math.max(0, meshCount - collisionMeshCount);
            long expectedManifestMeshCount = (long)manifestRecords * GeologyForgeConstants.LodCount;
            bool exactMeshSet = visualMeshCount == manifestMeshReferenceCount && manifestMeshReferenceCount == expectedManifestMeshCount;
            bool collisionProxySetValid = collisionMeshCount == manifestRecords && collisionMeshFailures == 0;
            bool prefabSetValid = prefabCount == manifestRecords && prefabFailures == 0;
            bool pass = meshCount > 0 && meshFailures == 0 && manifestValid && manifestRecords > 0u && !manifestFailure && exactMeshSet && collisionProxySetValid && prefabSetValid;
            StringBuilder builder = new StringBuilder(2048);
            builder.Append("{\n  \"agent\": \"1606\",\n  \"status\": \"");
            builder.Append(pass ? "STATIC_LAYOUT_AUDIT_PASS" : "STATIC_LAYOUT_AUDIT_FAIL");
            builder.Append("\",\n  \"meshCount\": ");
            builder.Append(meshCount);
            builder.Append(",\n  \"visualMeshCount\": ");
            builder.Append(visualMeshCount);
            builder.Append(",\n  \"meshFailures\": ");
            builder.Append(meshFailures);
            builder.Append(",\n  \"unmanifestedMeshCount\": ");
            builder.Append(unmanifestedMeshCount);
            builder.Append(",\n  \"collisionMeshCount\": ");
            builder.Append(collisionMeshCount);
            builder.Append(",\n  \"collisionMeshFailures\": ");
            builder.Append(collisionMeshFailures);
            builder.Append(",\n  \"prefabCount\": ");
            builder.Append(prefabCount);
            builder.Append(",\n  \"prefabFailures\": ");
            builder.Append(prefabFailures);
            builder.Append(",\n  \"manifestMeshReferenceCount\": ");
            builder.Append(manifestMeshReferenceCount);
            builder.Append(",\n  \"expectedManifestMeshCount\": ");
            builder.Append(expectedManifestMeshCount);
            builder.Append(",\n  \"exactMeshSet\": ");
            builder.Append(exactMeshSet ? "true" : "false");
            builder.Append(",\n  \"manifestValid\": ");
            builder.Append(manifestValid ? "true" : "false");
            builder.Append(",\n  \"manifestRecords\": ");
            builder.Append(manifestRecords);
            builder.Append(",\n  \"manifestBytes\": ");
            builder.Append(manifestBytes);
            builder.Append(",\n  \"manifestReason\": \"");
            builder.Append(Escape(manifestReason));
            builder.Append("\",\n  \"noOutput\": ");
            builder.Append(noOutput ? "true" : "false");
            builder.Append(",\n  \"vertexStrideBytes\": ");
            builder.Append(GeologyForgeConstants.VertexStrideBytes);
            builder.Append(",\n  \"failures\": [");
            if (failures.Length > 0)
            {
                builder.Append('\n');
                builder.Append(failures);
                builder.Append("  ");
            }

            builder.Append("]\n}\n");
            WriteAtomicText(GeologyForgeConstants.LayoutAuditReportPath, builder.ToString());
        }

        private static void AppendFailure(StringBuilder builder, string path, string reason)
        {
            if (builder.Length > 0)
                builder.Append(",\n");
            builder.Append("    { \"path\": \"");
            builder.Append(Escape(path));
            builder.Append("\", \"reason\": \"");
            builder.Append(Escape(reason));
            builder.Append("\" }\n");
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void WriteAtomicText(string path, string contents)
        {
            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            try
            {
                File.WriteAllText(tempPath, contents);
                if (File.Exists(path))
                {
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    File.Replace(tempPath, path, backupPath);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            catch
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                throw;
            }
        }
    }
}
