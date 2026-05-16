using UnityEngine;

namespace Hecton8.Core.Content
{
    /// <summary>
    /// Cheap AABB frustum gate to block heavy SDF or procedural visibility work when the proxy is off-screen.
    /// </summary>
    public abstract class VisibilityProxyBase : MonoBehaviour
    {
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
                Mathf.Abs(localSize.x * scale.x),
                Mathf.Abs(localSize.y * scale.y),
                Mathf.Abs(localSize.z * scale.z));
            _worldBounds = new Bounds(tr.TransformPoint(localCenter), size);
        }

        public abstract void ExecuteVisibleWork(float deltaTime);
    }
}
