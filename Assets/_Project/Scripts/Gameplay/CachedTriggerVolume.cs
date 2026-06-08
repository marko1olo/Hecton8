namespace Hecton8.Core
{
    using Unity.Mathematics;
    using UnityEngine;

    public enum CachedTriggerVolumeShape : byte
    {
        Radius = 0,
        Sphere = 1,
        Box = 2,
        Capsule = 3
    }

    public struct CachedTriggerVolume
    {
        public CachedTriggerVolumeShape Shape;
        public float3 LocalCenter;
        public float3 LocalHalfExtents;
        public float Radius;
        public float Height;
        public byte CapsuleAxis;

        public static CachedTriggerVolume FromCollider(Collider source, float fallbackRadius)
        {
            float safeFallback = ResolvePositiveFinite(fallbackRadius, 0.01f);
            CachedTriggerVolume volume = new CachedTriggerVolume
            {
                Shape = CachedTriggerVolumeShape.Radius,
                LocalCenter = float3.zero,
                LocalHalfExtents = new float3(safeFallback),
                Radius = safeFallback,
                Height = safeFallback * 2f,
                CapsuleAxis = 1
            };

            if (source is SphereCollider sphere)
            {
                volume.Shape = CachedTriggerVolumeShape.Sphere;
                volume.LocalCenter = ResolveFiniteFloat3(sphere.center, float3.zero);
                volume.Radius = ResolvePositiveFinite(sphere.radius, safeFallback);
                return volume;
            }

            if (source is BoxCollider box)
            {
                volume.Shape = CachedTriggerVolumeShape.Box;
                volume.LocalCenter = ResolveFiniteFloat3(box.center, float3.zero);
                float3 safeSize = ResolveFiniteFloat3(box.size, new float3(safeFallback * 2f));
                volume.LocalHalfExtents = math.max(safeSize * 0.5f, new float3(0.01f));
                return volume;
            }

            if (source is CapsuleCollider capsule)
            {
                volume.Shape = CachedTriggerVolumeShape.Capsule;
                volume.LocalCenter = ResolveFiniteFloat3(capsule.center, float3.zero);
                volume.Radius = ResolvePositiveFinite(capsule.radius, safeFallback);
                float safeHeight = ResolvePositiveFinite(capsule.height, volume.Radius * 2f);
                volume.Height = math.max(volume.Radius * 2f, safeHeight);
                volume.CapsuleAxis = (byte)math.clamp(capsule.direction, 0, 2);
                return volume;
            }

            return volume;
        }

        public bool Contains(Transform owner, Vector3 worldPoint)
        {
            if (owner == null ||
                !math.isfinite(worldPoint.x) ||
                !math.isfinite(worldPoint.y) ||
                !math.isfinite(worldPoint.z))
            {
                return false;
            }

            float3 localPoint = (float3)owner.InverseTransformPoint(worldPoint);
            if (!math.all(math.isfinite(localPoint)) || !IsFiniteVolume())
                return false;

            switch (Shape)
            {
                case CachedTriggerVolumeShape.Sphere:
                case CachedTriggerVolumeShape.Radius:
                    return math.lengthsq(localPoint - LocalCenter) <= Radius * Radius;
                case CachedTriggerVolumeShape.Box:
                    float3 boxDelta = math.abs(localPoint - LocalCenter);
                    return boxDelta.x <= LocalHalfExtents.x &&
                           boxDelta.y <= LocalHalfExtents.y &&
                           boxDelta.z <= LocalHalfExtents.z;
                case CachedTriggerVolumeShape.Capsule:
                    float3 capsulePoint = ClosestPointOnCapsuleSegment(localPoint);
                    return math.lengthsq(localPoint - capsulePoint) <= Radius * Radius;
                default:
                    return false;
            }
        }

        public Vector3 ResolveSurfacePoint(Transform owner, Vector3 worldPoint)
        {
            if (owner == null)
                return worldPoint;

            float3 localPoint = (float3)owner.InverseTransformPoint(worldPoint);
            if (!math.all(math.isfinite(localPoint)) || !IsFiniteVolume())
                return worldPoint;

            float3 closestLocal;
            switch (Shape)
            {
                case CachedTriggerVolumeShape.Sphere:
                case CachedTriggerVolumeShape.Radius:
                    closestLocal = ClosestPointOnSphere(localPoint);
                    break;
                case CachedTriggerVolumeShape.Box:
                    closestLocal = math.clamp(localPoint, LocalCenter - LocalHalfExtents, LocalCenter + LocalHalfExtents);
                    break;
                case CachedTriggerVolumeShape.Capsule:
                    float3 capsuleAxisPoint = ClosestPointOnCapsuleSegment(localPoint);
                    float3 capsuleDelta = localPoint - capsuleAxisPoint;
                    float capsuleDeltaSq = math.lengthsq(capsuleDelta);
                    closestLocal = capsuleDeltaSq > 0.000001f
                        ? capsuleAxisPoint + capsuleDelta * (Radius * math.rsqrt(capsuleDeltaSq))
                        : capsuleAxisPoint;
                    break;
                default:
                    closestLocal = localPoint;
                    break;
            }

            Vector3 closestWorld = owner.TransformPoint((Vector3)closestLocal);
            return IsFiniteVector3(closestWorld) ? closestWorld : worldPoint;
        }

        private float3 ClosestPointOnSphere(float3 localPoint)
        {
            float3 delta = localPoint - LocalCenter;
            float deltaSq = math.lengthsq(delta);
            if (deltaSq <= 0.000001f)
                return LocalCenter;

            return LocalCenter + delta * (Radius * math.rsqrt(deltaSq));
        }

        private float3 ClosestPointOnCapsuleSegment(float3 localPoint)
        {
            float3 closest = LocalCenter;
            float axisValue = Axis(localPoint - LocalCenter, CapsuleAxis);
            float segmentHalfLength = math.max(0f, (Height * 0.5f) - Radius);
            SetAxis(ref closest, CapsuleAxis, Axis(LocalCenter, CapsuleAxis) + math.clamp(axisValue, -segmentHalfLength, segmentHalfLength));
            return closest;
        }

        private static float Axis(float3 value, byte axis)
        {
            return axis == 0 ? value.x : axis == 2 ? value.z : value.y;
        }

        private static void SetAxis(ref float3 value, byte axis, float component)
        {
            if (axis == 0)
                value.x = component;
            else if (axis == 2)
                value.z = component;
            else
                value.y = component;
        }

        private bool IsFiniteVolume()
        {
            return math.all(math.isfinite(LocalCenter)) &&
                   math.all(math.isfinite(LocalHalfExtents)) &&
                   math.isfinite(Radius) &&
                   Radius > 0f &&
                   math.isfinite(Height) &&
                   Height > 0f;
        }

        private static float ResolvePositiveFinite(float value, float fallback)
        {
            float safeFallback = math.isfinite(fallback) ? math.max(0.01f, fallback) : 0.01f;
            return math.isfinite(value) ? math.max(0.01f, value) : safeFallback;
        }

        private static float3 ResolveFiniteFloat3(Vector3 value, float3 fallback)
        {
            float3 candidate = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(candidate)) ? candidate : fallback;
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }
    }
}
