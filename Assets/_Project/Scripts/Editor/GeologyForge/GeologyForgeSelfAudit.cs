using System;
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
            StringBuilder failures = new StringBuilder(1024);
            ValidateGeneratedMeshes(ref meshCount, ref meshFailures, failures);
            bool manifestValid = TryValidateManifest(out uint manifestRecords, out long manifestBytes, out string manifestReason);
            bool manifestRequired = meshCount > 0;
            bool manifestFailure = manifestRequired && !manifestValid;
            WriteReport(meshCount, meshFailures, manifestValid, manifestRecords, manifestBytes, manifestReason, manifestFailure, failures);
            Debug.Log($"[SHINOBU_208] Geology layout audit wrote {GeologyForgeConstants.LayoutAuditReportPath} meshes={meshCount} meshFailures={meshFailures} manifestValid={manifestValid}.");
        }

        private static void ValidateGeneratedMeshes(ref int meshCount, ref int meshFailures, StringBuilder failures)
        {
            if (!Directory.Exists(GeologyForgeConstants.MeshOutputFolder))
                return;

            string[] files = Directory.GetFiles(GeologyForgeConstants.MeshOutputFolder, "*.asset", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                    continue;

                meshCount++;
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

        private static bool TryValidateManifest(out uint recordCount, out long byteLength, out string reason)
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

            using (FileStream stream = new FileStream(GeologyForgeConstants.ManifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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
                    if (!ValidateManifestRecord(record))
                    {
                        reason = "BAD_RECORD";
                        return false;
                    }
                }
            }

            reason = "OK";
            return true;
        }

        private static bool ValidateManifestRecord(GeologyMeshManifestRecord record)
        {
            if (record.VertexStrideBytes != GeologyForgeConstants.VertexStrideBytes)
                return false;
            if ((record.Flags & GeologyForgeConstants.ManifestFlagBrgReady) == 0u)
                return false;
            if (record.Lod0Triangles < 0 || record.Lod1Triangles < 0 || record.Lod2Triangles < 0)
                return false;
            if (!math.all(math.isfinite(record.BoundsCenter)) || !math.all(math.isfinite(record.BoundsExtents)))
                return false;
            if (record.Lod0GuidHigh == 0UL && record.Lod0GuidLow == 0UL)
                return false;
            if (record.Lod1GuidHigh == 0UL && record.Lod1GuidLow == 0UL)
                return false;
            if (record.Lod2GuidHigh == 0UL && record.Lod2GuidLow == 0UL)
                return false;
            return true;
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
            bool manifestValid,
            uint manifestRecords,
            long manifestBytes,
            string manifestReason,
            bool manifestFailure,
            StringBuilder failures)
        {
            string folder = Path.GetDirectoryName(GeologyForgeConstants.LayoutAuditReportPath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            bool pass = meshFailures == 0 && !manifestFailure;
            StringBuilder builder = new StringBuilder(2048);
            builder.Append("{\n  \"agent\": \"SHINOBU_208\",\n  \"status\": \"");
            builder.Append(pass ? "STATIC_LAYOUT_AUDIT_PASS" : "STATIC_LAYOUT_AUDIT_FAIL");
            builder.Append("\",\n  \"meshCount\": ");
            builder.Append(meshCount);
            builder.Append(",\n  \"meshFailures\": ");
            builder.Append(meshFailures);
            builder.Append(",\n  \"manifestValid\": ");
            builder.Append(manifestValid ? "true" : "false");
            builder.Append(",\n  \"manifestRecords\": ");
            builder.Append(manifestRecords);
            builder.Append(",\n  \"manifestBytes\": ");
            builder.Append(manifestBytes);
            builder.Append(",\n  \"manifestReason\": \"");
            builder.Append(Escape(manifestReason));
            builder.Append("\",\n  \"vertexStrideBytes\": ");
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
