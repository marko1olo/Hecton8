using UnityEngine;

namespace Hecton8.Visor
{
    public sealed class DynamicDecalGizmoVisualizer : MonoBehaviour
    {
        [SerializeField] private bool drawDecalVolumes = true;
        [SerializeField, Range(1, 256)] private int maxDrawnVolumes = 64;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawDecalVolumes ||
                !DynamicDecalVaultRuntime.TryAcquireDecalBufferRead(out Unity.Collections.NativeArray<DecalInstanceDTO> decals, out _, out Vector3 cameraWorldPosition))
            {
                return;
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            try
            {
                int drawn = 0;
                for (int i = 0; i < decals.Length && drawn < maxDrawnVolumes; i++)
                {
                    DecalInstanceDTO decal = decals[i];
                    if ((decal.Flags & DynamicDecalFlags.Active) == 0u || decal.Opacity01 <= 0.0001f)
                        continue;

                    Unity.Mathematics.float4x4 source = decal.LocalToWorld;
                    Matrix4x4 matrix = default;
                    matrix.SetColumn(0, new Vector4(source.c0.x, source.c0.y, source.c0.z, source.c0.w));
                    matrix.SetColumn(1, new Vector4(source.c1.x, source.c1.y, source.c1.z, source.c1.w));
                    matrix.SetColumn(2, new Vector4(source.c2.x, source.c2.y, source.c2.z, source.c2.w));
                    matrix.SetColumn(
                        3,
                        new Vector4(
                            source.c3.x + cameraWorldPosition.x,
                            source.c3.y + cameraWorldPosition.y,
                            source.c3.z + cameraWorldPosition.z,
                            1f));

                    float opacity = Mathf.Clamp01(decal.Opacity01);
                    Gizmos.matrix = matrix;
                    Gizmos.color = new Color(1f, 0.35f, 0.08f, 0.2f + opacity * 0.55f);
                    Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                    drawn++;
                }
            }
            finally
            {
                DynamicDecalVaultRuntime.ReleaseDecalBufferRead();
                Gizmos.matrix = previousMatrix;
                Gizmos.color = previousColor;
            }
        }
#endif
    }
}
