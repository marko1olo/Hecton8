using System;
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor.GeologyForge
{
    internal static class GeologyVertexLayoutValidator
    {
        private static readonly VertexAttributeDescriptor[] _GeologyLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 0),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.UNorm16, 2, 0)
        };

        public static VertexAttributeDescriptor[] GetLayout()
        {
            return new[]
            {
                _GeologyLayout[0],
                _GeologyLayout[1],
                _GeologyLayout[2],
                _GeologyLayout[3]
            };
        }

        public static void ValidateStruct()
        {
            GeologyTetraExtractionLut.ValidateComplementWinding();

            int size = UnsafeUtility.SizeOf<GeologyVertex32>();
            if (size != GeologyForgeConstants.VertexStrideBytes)
                throw new InvalidOperationException($"GeologyVertex32 stride mismatch. Expected 32, got {size}.");

            int rawSize = UnsafeUtility.SizeOf<GeologyRawVertex>();
            if (rawSize != 64)
                throw new InvalidOperationException($"GeologyRawVertex stride mismatch. Expected 64, got {rawSize}.");

            int telemetrySize = UnsafeUtility.SizeOf<GeologyBakeTelemetryEntry>();
            if (telemetrySize != 64)
                throw new InvalidOperationException($"GeologyBakeTelemetryEntry stride mismatch. Expected 64, got {telemetrySize}.");

            int dumpHeaderSize = UnsafeUtility.SizeOf<GeologyBakeDumpHeader>();
            if (dumpHeaderSize != 32)
                throw new InvalidOperationException($"GeologyBakeDumpHeader stride mismatch. Expected 32, got {dumpHeaderSize}.");

            int manifestHeaderSize = UnsafeUtility.SizeOf<GeologyMeshManifestHeader>();
            if (manifestHeaderSize != 64)
                throw new InvalidOperationException($"GeologyMeshManifestHeader stride mismatch. Expected 64, got {manifestHeaderSize}.");

            int manifestRecordSize = UnsafeUtility.SizeOf<GeologyMeshManifestRecord>();
            if (manifestRecordSize != 128)
                throw new InvalidOperationException($"GeologyMeshManifestRecord stride mismatch. Expected 128, got {manifestRecordSize}.");

            ValidateOffset<GeologyVertex32>(nameof(GeologyVertex32.Position), 0);
            ValidateOffset<GeologyVertex32>(nameof(GeologyVertex32.Normal), 12);
            ValidateOffset<GeologyVertex32>(nameof(GeologyVertex32.ColorRgba), 24);
            ValidateOffset<GeologyVertex32>(nameof(GeologyVertex32.Uv0Packed), 28);
            ValidateOffset<GeologyRawVertex>(nameof(GeologyRawVertex.Position), 0);
            ValidateOffset<GeologyRawVertex>(nameof(GeologyRawVertex.Normal), 12);
            ValidateOffset<GeologyRawVertex>(nameof(GeologyRawVertex.Tangent), 24);
            ValidateOffset<GeologyRawVertex>(nameof(GeologyRawVertex.Uv), 40);
            ValidateOffset<GeologyRawVertex>(nameof(GeologyRawVertex.AmbientOcclusion), 48);
            ValidateOffset<GeologyRawVertex>(nameof(GeologyRawVertex.Flags), 52);
            ValidateOffset<GeologyRawVertex>(nameof(GeologyRawVertex.Padding0), 56);
            ValidateOffset<GeologyBakeTelemetryEntry>(nameof(GeologyBakeTelemetryEntry.SectorAup), 0);
            ValidateOffset<GeologyBakeTelemetryEntry>(nameof(GeologyBakeTelemetryEntry.Seed), 24);
            ValidateOffset<GeologyBakeTelemetryEntry>(nameof(GeologyBakeTelemetryEntry.Stage), 28);
            ValidateOffset<GeologyBakeTelemetryEntry>(nameof(GeologyBakeTelemetryEntry.StageMilliseconds), 32);
            ValidateOffset<GeologyBakeTelemetryEntry>(nameof(GeologyBakeTelemetryEntry.RawVertexCount), 36);
            ValidateOffset<GeologyBakeTelemetryEntry>(nameof(GeologyBakeTelemetryEntry.Lod0Triangles), 40);
            ValidateOffset<GeologyBakeTelemetryEntry>(nameof(GeologyBakeTelemetryEntry.Lod1Triangles), 44);
            ValidateOffset<GeologyBakeTelemetryEntry>(nameof(GeologyBakeTelemetryEntry.Lod2Triangles), 48);
            ValidateOffset<GeologyBakeTelemetryEntry>(nameof(GeologyBakeTelemetryEntry.WarningFlags), 52);
            ValidateOffset<GeologyBakeTelemetryEntry>(nameof(GeologyBakeTelemetryEntry.StateHash), 56);
            ValidateOffset<GeologyBakeTelemetryEntry>(nameof(GeologyBakeTelemetryEntry.DumpReason), 60);
            ValidateOffset<GeologyBakeDumpHeader>(nameof(GeologyBakeDumpHeader.Magic), 0);
            ValidateOffset<GeologyBakeDumpHeader>(nameof(GeologyBakeDumpHeader.EntryCount), 4);
            ValidateOffset<GeologyBakeDumpHeader>(nameof(GeologyBakeDumpHeader.EntrySize), 8);
            ValidateOffset<GeologyBakeDumpHeader>(nameof(GeologyBakeDumpHeader.Cursor), 12);
            ValidateOffset<GeologyBakeDumpHeader>(nameof(GeologyBakeDumpHeader.Reason), 16);
            ValidateOffset<GeologyBakeDumpHeader>(nameof(GeologyBakeDumpHeader.Reserved0), 20);
            ValidateOffset<GeologyBakeDumpHeader>(nameof(GeologyBakeDumpHeader.Reserved1), 24);
            ValidateOffset<GeologyMeshManifestHeader>(nameof(GeologyMeshManifestHeader.Magic), 0);
            ValidateOffset<GeologyMeshManifestHeader>(nameof(GeologyMeshManifestHeader.Version), 4);
            ValidateOffset<GeologyMeshManifestHeader>(nameof(GeologyMeshManifestHeader.RecordCount), 8);
            ValidateOffset<GeologyMeshManifestHeader>(nameof(GeologyMeshManifestHeader.RecordSize), 12);
            ValidateOffset<GeologyMeshManifestHeader>(nameof(GeologyMeshManifestHeader.HeaderSize), 16);
            ValidateOffset<GeologyMeshManifestHeader>(nameof(GeologyMeshManifestHeader.VertexStrideBytes), 20);
            ValidateOffset<GeologyMeshManifestHeader>(nameof(GeologyMeshManifestHeader.LodCount), 24);
            ValidateOffset<GeologyMeshManifestHeader>(nameof(GeologyMeshManifestHeader.Flags), 28);
            ValidateOffset<GeologyMeshManifestHeader>(nameof(GeologyMeshManifestHeader.Reserved0), 32);
            ValidateOffset<GeologyMeshManifestHeader>(nameof(GeologyMeshManifestHeader.Reserved1), 40);
            ValidateOffset<GeologyMeshManifestHeader>(nameof(GeologyMeshManifestHeader.Reserved2), 48);
            ValidateOffset<GeologyMeshManifestHeader>(nameof(GeologyMeshManifestHeader.Reserved3), 56);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.SectorAup), 0);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.Seed), 24);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.ProfileHash), 28);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.Lod0Triangles), 32);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.Lod1Triangles), 36);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.Lod2Triangles), 40);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.VertexStrideBytes), 44);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.BoundsCenter), 48);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.BoundsExtents), 60);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.Lod0GuidHigh), 72);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.Lod0GuidLow), 80);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.Lod1GuidHigh), 88);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.Lod1GuidLow), 96);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.Lod2GuidHigh), 104);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.Lod2GuidLow), 112);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.Flags), 120);
            ValidateOffset<GeologyMeshManifestRecord>(nameof(GeologyMeshManifestRecord.Variation), 124);
        }

        public static void ValidateMesh(Mesh mesh)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));

            int stride = mesh.GetVertexBufferStride(0);
            if (stride != GeologyForgeConstants.VertexStrideBytes || (stride & 3) != 0)
                throw new InvalidOperationException($"Mesh {mesh.name} has invalid vertex stride {stride}. Expected 32 and multiple of 4.");

            VertexAttributeDescriptor[] attributes = mesh.GetVertexAttributes();
            if (attributes.Length != _GeologyLayout.Length)
                throw new InvalidOperationException($"Mesh {mesh.name} has {attributes.Length} vertex attributes. Expected {_GeologyLayout.Length}.");

            for (int i = 0; i < _GeologyLayout.Length; i++)
            {
                VertexAttributeDescriptor expected = _GeologyLayout[i];
                VertexAttributeDescriptor actual = attributes[i];
                if (actual.attribute != expected.attribute ||
                    actual.format != expected.format ||
                    actual.dimension != expected.dimension ||
                    actual.stream != expected.stream)
                {
                    throw new InvalidOperationException($"Mesh {mesh.name} attribute {i} layout mismatch. Expected {expected.attribute}/{expected.format}/{expected.dimension}, got {actual.attribute}/{actual.format}/{actual.dimension}.");
                }
            }
        }

        private static void ValidateOffset<T>(string fieldName, int expectedOffset)
            where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException($"{typeof(T).Name}.{fieldName} field missing.");

            int actualOffset = UnsafeUtility.GetFieldOffset(field);
            if (actualOffset != expectedOffset)
                throw new InvalidOperationException($"{typeof(T).Name}.{fieldName} offset mismatch. Expected {expectedOffset}, got {actualOffset}.");
        }
    }
}
