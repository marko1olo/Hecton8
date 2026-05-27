#if UNITY_EDITOR
using System;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.UI;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Visor
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Visor/Visor Stencil Preview Gizmo")]
    public sealed class HectonVisorStencilPreviewGizmo : MonoBehaviour
    {
        private const int PreviewTargetCapacity = 3;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private Mesh visorMaskMesh;
        [SerializeField] private Vector3 maskLocalPosition = new Vector3(0f, 0f, 0.38f);
        [SerializeField] private Vector3 maskLocalEuler = Vector3.zero;
        [SerializeField] private Vector3 maskLocalScale = new Vector3(0.92f, 0.58f, 1f);
        [SerializeField] private bool drawStencilWire = true;
        [SerializeField] private bool drawArTargetRays = true;

        private void OnDrawGizmos()
        {
            Camera camera = targetCamera;
            if (camera == null)
                TryGetComponent(out camera);
            if (camera == null)
                return;

            Matrix4x4 matrix = camera.transform.localToWorldMatrix *
                               Matrix4x4.TRS(maskLocalPosition, Quaternion.Euler(maskLocalEuler), maskLocalScale);
            if (drawStencilWire)
            {
                Gizmos.color = new Color(0.36f, 0.95f, 1f, 0.72f);
                Vector3 worldPosition = camera.transform.TransformPoint(maskLocalPosition);
                Quaternion worldRotation = camera.transform.rotation * Quaternion.Euler(maskLocalEuler);
                if (visorMaskMesh != null)
                {
                    Gizmos.DrawWireMesh(visorMaskMesh, worldPosition, worldRotation, maskLocalScale);
                }
                else
                {
                    Matrix4x4 previousMatrix = Gizmos.matrix;
                    Gizmos.matrix = matrix;
                    Gizmos.DrawWireCube(Vector3.zero, new Vector3(2f, 1.3f, 0.02f));
                    Gizmos.matrix = previousMatrix;
                }
            }

            if (!drawArTargetRays || !Application.isPlaying)
                return;

            Span<ARWaypointOverlay.StencilTargetSourceDTO> targetScratch =
                stackalloc ARWaypointOverlay.StencilTargetSourceDTO[PreviewTargetCapacity];
            int count = ARWaypointOverlay.CopyStencilTargetSources(targetScratch, PreviewTargetCapacity);
            if (count <= 0)
                return;

            Vector3 runtimeCameraPosition = camera.transform.position;
            double3 originAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            AbsoluteUniversePosition cameraAup = AbsoluteUniversePosition.FromAbsolutePosition(
                originAup + new double3(runtimeCameraPosition.x, runtimeCameraPosition.y, runtimeCameraPosition.z));
            if (!cameraAup.IsFinite())
                return;

            Gizmos.color = Color.yellow;
            Vector3 origin = camera.transform.position;
            int limit = math.min(count, targetScratch.Length);
            for (int i = 0; i < limit; i++)
            {
                ARWaypointOverlay.StencilTargetSourceDTO target = targetScratch[i];
                if ((target.Flags & 1u) == 0u || !target.PositionAup.IsFinite())
                    continue;

                float3 local = AupPrecisionMath.LocalDeltaFloat3Clamped(
                    target.PositionAup.ToAbsoluteDouble3(),
                    cameraAup.ToAbsoluteDouble3(),
                    AupPrecisionMath.DefaultMaxLocalCastMeters,
                    float3.zero);
                Vector3 localVector = default;
                localVector.x = local.x;
                localVector.y = local.y;
                localVector.z = local.z;
                Vector3 world = origin + localVector;
                Gizmos.DrawLine(origin, world);
                Gizmos.DrawWireSphere(world, 0.15f);
            }
        }
    }
}
#endif
