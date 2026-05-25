namespace Hecton8.Interaction
{
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Runtime-owned per-tool VR grip offsets. Each tool prefab carries its own authored matrices.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/Physical Tool Grip Offsets")]
    public sealed class PhysicalToolGripOffsets : MonoBehaviour
    {
        private const int LeftIndex = 0;
        private const int RightIndex = 1;
        private const float MaximumGripOffsetMeters = 3f;

        [SerializeField] private Matrix4x4 leftHandGripOffset = Matrix4x4.identity;
        [SerializeField] private Matrix4x4 rightHandGripOffset = Matrix4x4.identity;
        [SerializeField] private bool applyOffsetsOnEquip = true;

        private float4x4 _leftGripOffset;
        private float4x4 _rightGripOffset;
        private bool _offsetsCached;

        public bool ApplyOffsetsOnEquip => applyOffsetsOnEquip;

        public bool TryApplyGripOffset(Transform toolTransform, PhysicalHandSide handSide)
        {
            if (!applyOffsetsOnEquip || toolTransform == null || !isActiveAndEnabled)
                return false;

            int index = handSide == PhysicalHandSide.Left ? LeftIndex : RightIndex;
            if (!TryReadGripOffset(index, out float4x4 offset))
                return false;

            ApplyOffset(toolTransform, offset);
            return true;
        }

        private void Awake()
        {
            CacheAuthoredOffsets();
        }

        private void OnEnable()
        {
            CacheAuthoredOffsets();
        }

        private void OnDisable()
        {
        }

        private void OnDestroy()
        {
            _offsetsCached = false;
        }

        private bool TryReadGripOffset(int index, out float4x4 offset)
        {
            if (!_offsetsCached)
                CacheAuthoredOffsets();

            switch (index)
            {
                case LeftIndex:
                    offset = _leftGripOffset;
                    return true;
                case RightIndex:
                    offset = _rightGripOffset;
                    return true;
                default:
                    offset = float4x4.identity;
                    return false;
            }
        }

        private void CacheAuthoredOffsets()
        {
            _leftGripOffset = ToFloat4x4(leftHandGripOffset);
            _rightGripOffset = ToFloat4x4(rightHandGripOffset);
            _offsetsCached = true;
        }

        private static float4x4 ToFloat4x4(Matrix4x4 matrix)
        {
            return new float4x4(
                new float4(matrix.m00, matrix.m10, matrix.m20, matrix.m30),
                new float4(matrix.m01, matrix.m11, matrix.m21, matrix.m31),
                new float4(matrix.m02, matrix.m12, matrix.m22, matrix.m32),
                new float4(matrix.m03, matrix.m13, matrix.m23, matrix.m33));
        }

        private static void ApplyOffset(Transform toolTransform, float4x4 offset)
        {
            if (!IsFiniteMatrix(offset))
                offset = float4x4.identity;

            Vector3 localPosition = SanitizeGripPosition(new Vector3(offset.c3.x, offset.c3.y, offset.c3.z));
            Vector3 localForward = new Vector3(offset.c2.x, offset.c2.y, offset.c2.z);
            Vector3 localUp = new Vector3(offset.c1.x, offset.c1.y, offset.c1.z);

            if (!IsFiniteVector(localForward) || localForward.sqrMagnitude < 0.000001f)
                localForward = Vector3.forward;
            if (!IsFiniteVector(localUp) || localUp.sqrMagnitude < 0.000001f)
                localUp = Vector3.up;

            toolTransform.localPosition = localPosition;
            Quaternion localRotation = ResolveBasisRotationNoTrig(localForward, localUp);
            toolTransform.localRotation = IsFiniteQuaternion(localRotation) ? localRotation : Quaternion.identity;
        }

        private static Quaternion ResolveBasisRotationNoTrig(Vector3 forward, Vector3 up)
        {
            float3 f = NormalizeVectorApproxNoSqrt((float3)forward, new float3(0f, 0f, 1f));
            float3 u = NormalizeVectorApproxNoSqrt((float3)up, new float3(0f, 1f, 0f));
            float3 r = NormalizeVectorApproxNoSqrt(math.cross(u, f), new float3(1f, 0f, 0f));
            u = NormalizeVectorApproxNoSqrt(math.cross(f, r), new float3(0f, 1f, 0f));

            float m00 = r.x;
            float m01 = u.x;
            float m02 = f.x;
            float m10 = r.y;
            float m11 = u.y;
            float m12 = f.y;
            float m20 = r.z;
            float m21 = u.z;
            float m22 = f.z;
            float trace = m00 + m11 + m22;

            float4 q;
            if (trace > 0f)
            {
                q = new float4(m21 - m12, m02 - m20, m10 - m01, 1f + trace);
            }
            else if (m00 >= m11 && m00 >= m22)
            {
                q = new float4(1f + m00 - m11 - m22, m01 + m10, m02 + m20, m21 - m12);
            }
            else if (m11 > m22)
            {
                q = new float4(m01 + m10, 1f + m11 - m00 - m22, m12 + m21, m02 - m20);
            }
            else
            {
                q = new float4(m02 + m20, m12 + m21, 1f + m22 - m00 - m11, m10 - m01);
            }

            q = NormalizeQuaternionNoSqrt(q);
            return new Quaternion(q.x, q.y, q.z, q.w);
        }

        private static float3 NormalizeVectorApproxNoSqrt(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            if (lenSq <= 0.000001f || !math.isfinite(lenSq))
                return fallback;

            return value * ApproximateInverseMagnitudeNoSqrt(value);
        }

        private static float4 NormalizeQuaternionNoSqrt(float4 value)
        {
            return value * ApproximateInverseMagnitudeNoSqrt(value);
        }

        private static float ApproximateInverseMagnitudeNoSqrt(float3 value)
        {
            float3 absValue = math.abs(value);
            float largest = math.cmax(absValue);
            float smallest = math.cmin(absValue);
            float middle = absValue.x + absValue.y + absValue.z - largest - smallest;
            float magnitude = largest + (middle * 0.375f) + (smallest * 0.125f);
            return math.rcp(math.max(magnitude, 0.000001f));
        }

        private static float ApproximateInverseMagnitudeNoSqrt(float4 value)
        {
            float4 absValue = math.abs(value);
            float largest = math.max(math.max(absValue.x, absValue.y), math.max(absValue.z, absValue.w));
            float smallest = math.min(math.min(absValue.x, absValue.y), math.min(absValue.z, absValue.w));
            float middleSum = absValue.x + absValue.y + absValue.z + absValue.w - largest - smallest;
            float magnitude = largest + (middleSum * 0.25f) + (smallest * 0.125f);
            return math.rcp(math.max(magnitude, 0.000001f));
        }

        private static Vector3 SanitizeGripPosition(Vector3 value)
        {
            if (!IsFiniteVector(value))
                return Vector3.zero;

            return (Vector3)math.clamp(
                (float3)value,
                new float3(-MaximumGripOffsetMeters),
                new float3(MaximumGripOffsetMeters));
        }

        private static bool IsFiniteMatrix(float4x4 value)
        {
            return math.all(math.isfinite(value.c0)) &&
                   math.all(math.isfinite(value.c1)) &&
                   math.all(math.isfinite(value.c2)) &&
                   math.all(math.isfinite(value.c3));
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            return math.all(math.isfinite(new float4(value.x, value.y, value.z, value.w)));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            leftHandGripOffset = SanitizeGripMatrix(leftHandGripOffset);
            rightHandGripOffset = SanitizeGripMatrix(rightHandGripOffset);
            CacheAuthoredOffsets();
        }

        private static Matrix4x4 SanitizeGripMatrix(Matrix4x4 matrix)
        {
            float4x4 value = ToFloat4x4(matrix);
            if (!IsFiniteMatrix(value))
                return Matrix4x4.identity;

            Vector3 clampedPosition = SanitizeGripPosition(new Vector3(matrix.m03, matrix.m13, matrix.m23));
            matrix.m03 = clampedPosition.x;
            matrix.m13 = clampedPosition.y;
            matrix.m23 = clampedPosition.z;
            return matrix;
        }
#endif
    }
}
