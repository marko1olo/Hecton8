using Hecton8.World;
using UnityEngine;

namespace Hecton8.Interaction
{
    /// <summary>
    /// Optional receiver for forward KCC hand-placement probes on repairable interactables.
    /// </summary>
    public interface IKinematicRepairTarget
    {
        bool TryResolveRepairSnapPoints(
            Vector3 runtimeHitPoint,
            out AbsoluteUniversePosition leftHandAup,
            out AbsoluteUniversePosition rightHandAup,
            out Quaternion toolRotation);

        bool TryResolveKinematicRepairSnap(in KinematicRepairTargetProbe probe, out KinematicRepairSnapPoint snapPoint);
    }

    public readonly struct KinematicRepairTargetProbe
    {
        public readonly AbsoluteUniversePosition RayOriginAup;
        public readonly AbsoluteUniversePosition HitAup;
        public readonly Vector3 RayDirection;
        public readonly Vector3 HitNormal;
        public readonly float HitDistance;
        public readonly int ColliderInstanceId;

        public KinematicRepairTargetProbe(
            AbsoluteUniversePosition rayOriginAup,
            AbsoluteUniversePosition hitAup,
            Vector3 rayDirection,
            Vector3 hitNormal,
            float hitDistance,
            int colliderInstanceId)
        {
            RayOriginAup = rayOriginAup;
            HitAup = hitAup;
            RayDirection = rayDirection;
            HitNormal = hitNormal;
            HitDistance = hitDistance;
            ColliderInstanceId = colliderInstanceId;
        }
    }

    public struct KinematicRepairSnapPoint
    {
        public AbsoluteUniversePosition AnchorAup;
        public AbsoluteUniversePosition LeftHandAup;
        public AbsoluteUniversePosition RightHandAup;
        public Vector3 RuntimePosition;
        public Vector3 SurfaceNormal;
        public Quaternion ToolRotation;
        public float HitDistance;
        public float Blend;
        public int ColliderInstanceId;
    }
}
