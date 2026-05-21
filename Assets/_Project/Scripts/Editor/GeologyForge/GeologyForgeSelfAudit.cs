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
        [MenuItem("HECTON-8/Geology Forge/Run Layout Self Audit", false, 182)]
        public static void RunAndWriteReport()
        {
            GeologyVertexLayoutValidator.ValidateStruct();
            int meshCount = 0;
            int meshFailures = 0;
            int unmanifestedMeshCount = 0;
            StringBuilder failures = new StringBuilder(1024);
            HashSet<string> manifestMeshPaths = new HashSet<string>(StringComparer.Ordinal);
            bool manifestValid = TryValidateManifest(manifestMeshPaths, out uint manifestRecords, out long manifestBytes, out string manifestReason);
            ValidateGeneratedMeshes(manifestMeshPaths, ref meshCount, ref meshFailures, ref unmanifestedMeshCount, failures);
            bool noOutput = meshCount == 0 || manifestRecords == 0u;
            bool manifestFailure = !manifestValid || manifestRecords == 0u;
            WriteReport(meshCount, meshFailures, unmanifestedMeshCount, manifestMeshPaths.Count, manifestValid, manifestRecords, manifestBytes, manifestReason, manifestFailure, noOutput, failures);
            Debug.Log($"[SHINOBU_208] Geology layout audit wrote {GeologyForgeConstants.LayoutAuditReportPath} meshes={meshCount} meshFailures={meshFailures} manifestValid={manifestValid} noOutput={noOutput}.");
        }

        private static void ValidateGeneratedMeshes(HashSet<string> manifestMeshPaths, ref int meshCount, ref int meshFailures, ref int unmanifestedMeshCount, StringBuilder failures)
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

            long expectedManifestMeshCount = (long)manifestRecords * GeologyForgeConstants.LodCount;
            bool exactMeshSet = meshCount == manifestMeshReferenceCount && manifestMeshReferenceCount == expectedManifestMeshCount;
            bool pass = meshCount > 0 && meshFailures == 0 && manifestValid && manifestRecords > 0u && !manifestFailure && exactMeshSet;
            StringBuilder builder = new StringBuilder(2048);
            builder.Append("{\n  \"agent\": \"SHINOBU_208\",\n  \"status\": \"");
            builder.Append(pass ? "STATIC_LAYOUT_AUDIT_PASS" : "STATIC_LAYOUT_AUDIT_FAIL");
            builder.Append("\",\n  \"meshCount\": ");
            builder.Append(meshCount);
            builder.Append(",\n  \"meshFailures\": ");
            builder.Append(meshFailures);
            builder.Append(",\n  \"unmanifestedMeshCount\": ");
            builder.Append(unmanifestedMeshCount);
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
