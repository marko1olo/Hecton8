using UnityEngine;

namespace Hecton8.Caves
{
    internal static class CaveRuntimeBoundsUtility
    {
        public static bool TryResolveLocalVolumeBounds(HectonVoxelVolume volume, CavePreset preset, out Bounds bounds)
        {
            MeshFilter meshFilter = volume != null ? volume.CachedMeshFilter : null;
            if (meshFilter != null)
            {
                Mesh sharedMesh = meshFilter.sharedMesh;
                if (sharedMesh != null && sharedMesh.bounds.size.sqrMagnitude > 0.01f)
                {
                    bounds = sharedMesh.bounds;
                    return true;
                }
            }

            float fallbackSize = 16f;
            if (preset != null)
                fallbackSize = Mathf.Max(8f, preset.gridDimension * Mathf.Max(0.5f, preset.voxelSize) * 0.42f);

            bounds = new Bounds(Vector3.zero, new Vector3(fallbackSize, fallbackSize * 0.62f, fallbackSize));
            return true;
        }
    }
}
