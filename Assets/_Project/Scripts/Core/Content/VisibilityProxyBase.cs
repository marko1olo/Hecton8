using UnityEngine;

namespace Hecton8.Core.Content
{
    /// <summary>
    /// Cheap AABB frustum gate to block heavy SDF or procedural visibility work when the proxy is off-screen.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class VisibilityProxyBase : MonoBehaviour
    {
        private const float MinVisibilityExtentMeters = 0.01f;
        private const float MaxVisibilityExtentMeters = 10000f;

        [SerializeField] private Vector3 localCenter;
        [SerializeField] private Vector3 localSize = Vector3.one;

        // COLD ALLOC: Plane[6] - reusable camera frustum planes for AABB gate - owner: VisibilityProxyBase
        private readonly Plane[] _frustumPlanes = new Plane[6];
        private Bounds _worldBounds;

        public Bounds LastWorldBounds => _worldBounds;

        public bool ShouldRunHeavyMath(Camera camera)
        {
            if (camera == null)
                return false;

            UpdateWorldBounds();
            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            return GeometryUtility.TestPlanesAABB(_frustumPlanes, _worldBounds);
        }

        public bool ShouldRunHeavyMath(Plane[] frustumPlanes)
        {
            if (frustumPlanes == null || frustumPlanes.Length < 6)
                return false;

            UpdateWorldBounds();
            return GeometryUtility.TestPlanesAABB(frustumPlanes, _worldBounds);
        }

        protected void UpdateWorldBounds()
        {
            Transform tr = transform;
            Vector3 scale = tr.lossyScale;
            Vector3 size = new Vector3(
                SanitizeExtent(localSize.x * scale.x),
                SanitizeExtent(localSize.y * scale.y),
                SanitizeExtent(localSize.z * scale.z));
            Vector3 center = tr.TransformPoint(localCenter);
            if (!IsFinite(center.x) || !IsFinite(center.y) || !IsFinite(center.z))
            {
                center = tr.position;
                if (!IsFinite(center.x) || !IsFinite(center.y) || !IsFinite(center.z))
                    center = Vector3.zero;
            }

            _worldBounds = new Bounds(center, size);
        }

        public abstract void ExecuteVisibleWork(float deltaTime);

        private static float SanitizeExtent(float value)
        {
            if (!IsFinite(value))
                return MinVisibilityExtentMeters;

            float absolute = Mathf.Abs(value);
            if (absolute < MinVisibilityExtentMeters)
                return MinVisibilityExtentMeters;
            if (absolute > MaxVisibilityExtentMeters)
                return MaxVisibilityExtentMeters;
            return absolute;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
