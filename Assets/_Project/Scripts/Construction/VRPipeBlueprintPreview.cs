using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Construction/VR Pipe Blueprint Preview")]
    public sealed class VRPipeBlueprintPreview : MonoBehaviour, ILateFrameTickable
    {
        private const int ControlPointCount = 4;
        private const int MaxPreviewInstances = 64;
        private const uint StateMatricesDirty = 1u << 0;
        private const float PointDirtyDistanceSq = 0.000025f;

        [Header("Preview")]
        [SerializeField] private Mesh segmentMesh;
        [SerializeField] private Material previewMaterial;
        [SerializeField] private bool previewActive;
        [SerializeField, Min(0.01f)] private float segmentLengthMeters = 0.35f;
        [SerializeField, Min(0.001f)] private float segmentRadiusMeters = 0.035f;
        [SerializeField] private Camera targetCamera;

        [Header("Control Points")]
        [SerializeField] private Transform point0;
        [SerializeField] private Transform point1;
        [SerializeField] private Transform point2;
        [SerializeField] private Transform point3;

        private readonly Vector3[] _runtimePoints = new Vector3[ControlPointCount]; // COLD ALLOC: Vector3[4] - pipe blueprint control points - owner: VRPipeBlueprintPreview
        private readonly bool[] _hasRuntimePoint = new bool[ControlPointCount]; // COLD ALLOC: bool[4] - runtime point validity - owner: VRPipeBlueprintPreview
        private readonly Matrix4x4[] _matrices = new Matrix4x4[MaxPreviewInstances]; // COLD ALLOC: Matrix4x4[64] - instanced pipe blueprint matrices - owner: VRPipeBlueprintPreview
        private Vector3 _cachedPoint0;
        private Vector3 _cachedPoint1;
        private Vector3 _cachedPoint2;
        private Vector3 _cachedPoint3;
        private float _cachedSegmentLengthMeters;
        private float _cachedSegmentRadiusMeters;
        private int _cachedMatrixCount;
        private uint _stateFlags = StateMatricesDirty;
        private bool _registeredLateFrame;

        public bool PreviewActive
        {
            get => previewActive;
            set => previewActive = value;
        }

        private void OnEnable()
        {
            TryRegisterLateFrameTickable();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrameTickable();
        }

        public void LateFrameTick()
        {
            if (!previewActive || segmentMesh == null || previewMaterial == null)
                return;

            if (ShouldRebuildMatrices())
                RebuildMatrixCache();

            if (_cachedMatrixCount <= 0)
                return;

            Graphics.DrawMeshInstanced(
                segmentMesh,
                0,
                previewMaterial,
                _matrices,
                _cachedMatrixCount,
                null,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                targetCamera,
                LightProbeUsage.Off,
                null);
        }

        public void SetPreviewPoint(int index, Vector3 runtimePosition)
        {
            if ((uint)index >= ControlPointCount)
                return;

            if (!_hasRuntimePoint[index] || (runtimePosition - _runtimePoints[index]).sqrMagnitude > PointDirtyDistanceSq)
                _stateFlags |= StateMatricesDirty;

            _runtimePoints[index] = runtimePosition;
            _hasRuntimePoint[index] = true;
        }

        public void ClearRuntimePoints()
        {
            bool hadRuntimePoint = false;
            for (int i = 0; i < ControlPointCount; i++)
            {
                hadRuntimePoint |= _hasRuntimePoint[i];
                _hasRuntimePoint[i] = false;
            }

            if (hadRuntimePoint)
                _stateFlags |= StateMatricesDirty;
        }

        private bool ShouldRebuildMatrices()
        {
            Vector3 p0 = ResolvePoint(0, point0);
            Vector3 p1 = ResolvePoint(1, point1);
            Vector3 p2 = ResolvePoint(2, point2);
            Vector3 p3 = ResolvePoint(3, point3);

            if ((_stateFlags & StateMatricesDirty) != 0u ||
                (_cachedPoint0 - p0).sqrMagnitude > PointDirtyDistanceSq ||
                (_cachedPoint1 - p1).sqrMagnitude > PointDirtyDistanceSq ||
                (_cachedPoint2 - p2).sqrMagnitude > PointDirtyDistanceSq ||
                (_cachedPoint3 - p3).sqrMagnitude > PointDirtyDistanceSq ||
                math.abs(_cachedSegmentLengthMeters - segmentLengthMeters) > 0.0001f ||
                math.abs(_cachedSegmentRadiusMeters - segmentRadiusMeters) > 0.0001f)
            {
                _cachedPoint0 = p0;
                _cachedPoint1 = p1;
                _cachedPoint2 = p2;
                _cachedPoint3 = p3;
                _cachedSegmentLengthMeters = segmentLengthMeters;
                _cachedSegmentRadiusMeters = segmentRadiusMeters;
                return true;
            }

            return false;
        }

        private void RebuildMatrixCache()
        {
            int count = 0;
            AppendSpan(_cachedPoint0, _cachedPoint1, ref count);
            AppendSpan(_cachedPoint1, _cachedPoint2, ref count);
            AppendSpan(_cachedPoint2, _cachedPoint3, ref count);
            _cachedMatrixCount = count;
            _stateFlags &= ~StateMatricesDirty;
        }

        private Vector3 ResolvePoint(int index, Transform authoredPoint)
        {
            if (authoredPoint != null)
                return authoredPoint.position;

            return _hasRuntimePoint[index] ? _runtimePoints[index] : transform.position;
        }

        private void AppendSpan(Vector3 start, Vector3 end, ref int count)
        {
            if (count >= MaxPreviewInstances)
                return;

            Vector3 delta = end - start;
            float lengthSq = delta.sqrMagnitude;
            if (lengthSq <= 0.000001f)
                return;

            float invLength = math.rsqrt(lengthSq);
            float length = lengthSq * invLength;
            Vector3 direction = delta * invLength;
            int segmentCount = math.clamp((int)math.ceil(length / math.max(0.01f, segmentLengthMeters)), 1, MaxPreviewInstances - count);
            float stepLength = length / segmentCount;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction);
            Vector3 scale = new Vector3(segmentRadiusMeters, stepLength * 0.5f, segmentRadiusMeters);

            for (int i = 0; i < segmentCount && count < MaxPreviewInstances; i++)
            {
                float offset = (i + 0.5f) * stepLength;
                Vector3 midpoint = start + direction * offset;
                _matrices[count] = Matrix4x4.TRS(midpoint, rotation, scale);
                count++;
            }
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = SystemDispatcher.GetLateFrameLane(PriorityLayer.Player).Contains(this);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrame = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (segmentLengthMeters < 0.01f)
                segmentLengthMeters = 0.01f;
            if (segmentRadiusMeters < 0.001f)
                segmentRadiusMeters = 0.001f;
            _stateFlags |= StateMatricesDirty;
        }
#endif
    }
}
