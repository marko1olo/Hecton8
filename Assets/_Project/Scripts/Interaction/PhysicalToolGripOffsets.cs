namespace Hecton8.Interaction
{
    using Hecton8.Core;
    using Unity.Collections;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Runtime-owned per-tool VR grip offsets. Each tool prefab carries its own authored matrices.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Interaction/Physical Tool Grip Offsets")]
    public sealed class PhysicalToolGripOffsets : MonoBehaviour
    {
        private const int GripOffsetCount = 2;
        private const int LeftIndex = 0;
        private const int RightIndex = 1;

        [SerializeField] private Matrix4x4 leftHandGripOffset = Matrix4x4.identity;
        [SerializeField] private Matrix4x4 rightHandGripOffset = Matrix4x4.identity;
        [SerializeField] private bool applyOffsetsOnEquip = true;

        private NativeArray<float4x4> _gripOffsets;
        private bool _allocated;

        public bool ApplyOffsetsOnEquip => applyOffsetsOnEquip;

        public bool TryApplyGripOffset(Transform toolTransform, PhysicalHandSide handSide)
        {
            if (!applyOffsetsOnEquip || toolTransform == null)
                return false;

            EnsureAllocated();
            int index = handSide == PhysicalHandSide.Left ? LeftIndex : RightIndex;
            ApplyOffset(toolTransform, _gripOffsets[index]);
            return true;
        }

        private void Awake()
        {
            EnsureAllocated();
            WriteAuthoredOffsets();
        }

        private void OnEnable()
        {
            EnsureAllocated();
            WriteAuthoredOffsets();
        }

        private void OnDestroy()
        {
            if (_gripOffsets.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_gripOffsets);
                _gripOffsets.Dispose();
            }

            _allocated = false;
        }

        private void EnsureAllocated()
        {
            if (_allocated && _gripOffsets.IsCreated)
                return;

            _gripOffsets = new NativeArray<float4x4>(GripOffsetCount, Allocator.Persistent); // COLD ALLOC: NativeArray<float4x4>[2] - VR hand grip offsets - owner: PhysicalToolGripOffsets
            NativeMemorySentinel.RegisterNativeArray(_gripOffsets, nameof(PhysicalToolGripOffsets), nameof(_gripOffsets), NativeAllocationLifetime.Session);
            _allocated = true;
            WriteAuthoredOffsets();
        }

        private void WriteAuthoredOffsets()
        {
            if (!_gripOffsets.IsCreated)
                return;

            _gripOffsets[LeftIndex] = ToFloat4x4(leftHandGripOffset);
            _gripOffsets[RightIndex] = ToFloat4x4(rightHandGripOffset);
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
            Vector3 localPosition = new Vector3(offset.c3.x, offset.c3.y, offset.c3.z);
            Vector3 localForward = new Vector3(offset.c2.x, offset.c2.y, offset.c2.z);
            Vector3 localUp = new Vector3(offset.c1.x, offset.c1.y, offset.c1.z);

            if (localForward.sqrMagnitude < 0.000001f)
                localForward = Vector3.forward;
            if (localUp.sqrMagnitude < 0.000001f)
                localUp = Vector3.up;

            Vector3 normalizedForward = (Vector3)math.normalizesafe((float3)localForward, new float3(0f, 0f, 1f));
            Vector3 normalizedUp = (Vector3)math.normalizesafe((float3)localUp, new float3(0f, 1f, 0f));
            toolTransform.localPosition = localPosition;
            toolTransform.localRotation = Quaternion.LookRotation(normalizedForward, normalizedUp);
        }
    }
}
