#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Text;
using Hecton8.World.VoxelTerrainSeamBinder;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World.VoxelTerrainSeamBinder.Editor
{
    [InitializeOnLoad]
    public static class VoxelTerrainSeamLayoutValidator
    {
        private const int MinimumStructAlignmentBytes = 8;
        private const int SeamBindVertexStrideBytes = 32;
        private const int BoundaryVertexStrideBytes = 64;
        private const int SnapResultStrideBytes = 64;
        private const int BindingProfileStrideBytes = 64;
        private const int BindCounterStrideBytes = 64;
        private const int TelemetryStrideBytes = 64;
        private const int RollbackFenceStrideBytes = 32;
        private const int SubMeshIndexRangeStrideBytes = 16;
        private const int SeamEdgeStrideBytes = 24;

        static VoxelTerrainSeamLayoutValidator()
        {
            if (!Validate(logSuccess: false))
                throw new InvalidOperationException("[SHINOBU_246] Voxel terrain seam layout validation failed.");
        }

        [MenuItem("HECTON-8/Voxel Terrain Seam Binder/Validate Layouts")]
        public static void ValidateMenu()
        {
            Validate(logSuccess: true);
        }

        public static bool Validate(bool logSuccess)
        {
            bool ok = true;
            ok &= ValidateSize<SeamBindVertex32>(SeamBindVertexStrideBytes);
            ok &= ValidateOffset<SeamBindVertex32>(nameof(SeamBindVertex32.Position), 0);
            ok &= ValidateOffset<SeamBindVertex32>(nameof(SeamBindVertex32.Normal), 12);
            ok &= ValidateOffset<SeamBindVertex32>(nameof(SeamBindVertex32.PackedColor), 24);
            ok &= ValidateOffset<SeamBindVertex32>(nameof(SeamBindVertex32.PackedUv0), 28);
            ok &= ValidateSize<SeamBoundaryVertex64>(BoundaryVertexStrideBytes);
            ok &= ValidateOffset<SeamBoundaryVertex64>(nameof(SeamBoundaryVertex64.Aup), 0);
            ok &= ValidateOffset<SeamBoundaryVertex64>(nameof(SeamBoundaryVertex64.LocalPosition), 24);
            ok &= ValidateOffset<SeamBoundaryVertex64>(nameof(SeamBoundaryVertex64.Normal), 36);
            ok &= ValidateOffset<SeamBoundaryVertex64>(nameof(SeamBoundaryVertex64.VertexIndex), 48);
            ok &= ValidateSize<SeamSnapResult64>(SnapResultStrideBytes);
            ok &= ValidateOffset<SeamSnapResult64>(nameof(SeamSnapResult64.OriginalLocalPosition), 0);
            ok &= ValidateOffset<SeamSnapResult64>(nameof(SeamSnapResult64.VoxelVertexIndex), 12);
            ok &= ValidateOffset<SeamSnapResult64>(nameof(SeamSnapResult64.SnappedLocalPosition), 16);
            ok &= ValidateOffset<SeamSnapResult64>(nameof(SeamSnapResult64.DistanceMeters), 28);
            ok &= ValidateOffset<SeamSnapResult64>(nameof(SeamSnapResult64.BlendedNormal), 32);
            ok &= ValidateSize<SeamBindingProfileDTO>(BindingProfileStrideBytes);
            ok &= ValidateSize<SeamBindCounters64>(BindCounterStrideBytes);
            ok &= ValidateSize<SeamBindTelemetryEntry>(TelemetryStrideBytes);
            ok &= ValidateSize<SeamMeshRollbackFenceDTO>(RollbackFenceStrideBytes);
            ok &= ValidateOffset<SeamMeshRollbackFenceDTO>(nameof(SeamMeshRollbackFenceDTO.TerrainMeshHash), 0);
            ok &= ValidateOffset<SeamMeshRollbackFenceDTO>(nameof(SeamMeshRollbackFenceDTO.VoxelMeshHash), 4);
            ok &= ValidateOffset<SeamMeshRollbackFenceDTO>(nameof(SeamMeshRollbackFenceDTO.StitchedMeshHash), 8);
            ok &= ValidateOffset<SeamMeshRollbackFenceDTO>(nameof(SeamMeshRollbackFenceDTO.RollbackExcluded), 12);
            ok &= ValidateOffset<SeamMeshRollbackFenceDTO>(nameof(SeamMeshRollbackFenceDTO.Magic), 16);
            ok &= ValidateOffset<SeamMeshRollbackFenceDTO>(nameof(SeamMeshRollbackFenceDTO.Version), 20);
            ok &= ValidateOffset<SeamMeshRollbackFenceDTO>(nameof(SeamMeshRollbackFenceDTO.EndianMarker), 24);
            ok &= ValidateOffset<SeamMeshRollbackFenceDTO>(nameof(SeamMeshRollbackFenceDTO.Reserved), 28);
            ok &= ValidateSize<SeamSubMeshIndexRangeDTO>(SubMeshIndexRangeStrideBytes);
            ok &= ValidateSize<SeamEdgeDTO>(SeamEdgeStrideBytes);
            ok &= ValidateVertexStride();

            if (ok && logSuccess)
                Debug.Log("[SHINOBU_246] Voxel terrain seam DTO and 32-byte mesh vertex layout validated.");

            return ok;
        }

        private static bool ValidateVertexStride()
        {
            int stride =
                AttributeBytes(new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0)) +
                AttributeBytes(new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0)) +
                AttributeBytes(new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 0)) +
                AttributeBytes(new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.UNorm16, 2, 0));

            if (stride == VoxelTerrainSeamConstants.StitchedVertexStrideBytes && (stride & 3) == 0)
                return true;

            LogVertexStrideMismatch(stride);
            return false;
        }

        private static int AttributeBytes(VertexAttributeDescriptor descriptor)
        {
            int componentBytes;
            switch (descriptor.format)
            {
                case VertexAttributeFormat.UNorm8:
                case VertexAttributeFormat.SNorm8:
                case VertexAttributeFormat.UInt8:
                case VertexAttributeFormat.SInt8:
                    componentBytes = 1;
                    break;
                case VertexAttributeFormat.Float16:
                case VertexAttributeFormat.UNorm16:
                case VertexAttributeFormat.SNorm16:
                case VertexAttributeFormat.UInt16:
                case VertexAttributeFormat.SInt16:
                    componentBytes = 2;
                    break;
                default:
                    componentBytes = 4;
                    break;
            }

            return componentBytes * descriptor.dimension;
        }

        private static bool ValidateSize<T>(int expected) where T : struct
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed == expected && (observed & (MinimumStructAlignmentBytes - 1)) == 0)
                return true;

            LogSizeMismatch(typeof(T).Name, expected, observed);
            return false;
        }

        private static bool ValidateOffset<T>(string fieldName, int expected) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int observed = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (observed == expected)
                return true;

            LogOffsetMismatch(typeof(T).Name, fieldName, expected, observed);
            return false;
        }

        private static void LogVertexStrideMismatch(int observed)
        {
            StringBuilder builder = new StringBuilder(72);
            builder.Append("[SHINOBU_246] Vertex stride mismatch: expected 32 observed ");
            builder.Append(observed);
            Debug.LogError(builder.ToString());
        }

        private static void LogSizeMismatch(string typeName, int expected, int observed)
        {
            StringBuilder builder = new StringBuilder(96);
            builder.Append("[SHINOBU_246] Layout size mismatch: ");
            builder.Append(typeName);
            builder.Append(" expected ");
            builder.Append(expected);
            builder.Append(" observed ");
            builder.Append(observed);
            Debug.LogError(builder.ToString());
        }

        private static void LogOffsetMismatch(string typeName, string fieldName, int expected, int observed)
        {
            StringBuilder builder = new StringBuilder(112);
            builder.Append("[SHINOBU_246] Layout offset mismatch: ");
            builder.Append(typeName);
            builder.Append('.');
            builder.Append(fieldName);
            builder.Append(" expected ");
            builder.Append(expected);
            builder.Append(" observed ");
            builder.Append(observed);
            Debug.LogError(builder.ToString());
        }
    }
}
#endif
