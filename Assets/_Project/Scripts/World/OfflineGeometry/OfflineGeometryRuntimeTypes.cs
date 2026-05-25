using System.Runtime.InteropServices;

namespace Hecton8.World.OfflineGeometry
{
    internal static class OfflineGeometryRuntimeTypesLayout
    {
        public const int LodConfigurationDTOStrideBytes = 16;
    }

    /// <summary>
    /// Aligned immutable LOD configuration emitted by the offline geometry baker.
    /// Runtime systems may copy this record into NativeArray or BRG metadata without
    /// relying on packed or reflection-based layout.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = OfflineGeometryRuntimeTypesLayout.LodConfigurationDTOStrideBytes)]
    public struct LodConfigurationDTO
    {
        /// <summary>Screen-relative transition height for LOD1.</summary>
        [FieldOffset(0)] public float Lod1Threshold;

        /// <summary>Screen-relative transition height for LOD2.</summary>
        [FieldOffset(4)] public float Lod2Threshold;

        /// <summary>Stable hash of the generated LOD1 mesh asset path.</summary>
        [FieldOffset(8)] public uint Lod1MeshHash;

        /// <summary>Stable hash of the generated LOD2 mesh asset path.</summary>
        [FieldOffset(12)] public uint Lod2MeshHash;
    }
}
