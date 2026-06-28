using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    [Flags]
    public enum DroneBoneSolverFlags : byte
    {
        None = 0,
        Active = 1,
        Chassis = 2,
        ServiceArm = 4,
        Sensor = 8,
        Thruster = 16,
        VisualOnly = 32
    }

    [Flags]
    public enum DroneBoneTierMask : byte
    {
        Low = 1,
        Middle = 2,
        High = 4,
        Ultra = 8,
        All = Low | Middle | High | Ultra
    }

    [Serializable]
    public struct DroneBoneJointDescriptor
    {
        public int BoneIndex;
        public int ParentIndex;
        public uint BoneHash;
        public DroneBoneSolverFlags SolverFlags;
        public DroneBoneTierMask TierMask;
        public Vector3 BindLocalPosition;
        public Quaternion BindLocalRotation;
        public Vector3 LocalAxis;
        public Vector3 LimitPlaneNormal;
        public float MinAngleDegrees;
        public float MaxAngleDegrees;
        public float Stiffness;
        public float Damping;
        public float SolverWeight;
        public Vector3 VisualOverkillOffset;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct DroneBoneJointRuntimeData
    {
        [FieldOffset(0)] public int BoneIndex;
        [FieldOffset(4)] public int ParentIndex;
        [FieldOffset(8)] public uint BoneHash;
        [FieldOffset(12)] public byte SolverFlags;
        [FieldOffset(13)] public byte TierMask;
        [FieldOffset(14)] public ushort Reserved0;
        [FieldOffset(16)] public float3 BindLocalPosition;
        [FieldOffset(28)] public float3 LocalAxis;
        [FieldOffset(40)] public quaternion BindLocalRotation;
        [FieldOffset(56)] public float MinAngleDegrees;
        [FieldOffset(60)] public float MaxAngleDegrees;
        [FieldOffset(64)] public float Stiffness;
        [FieldOffset(68)] public float Damping;
        [FieldOffset(72)] public float3 LimitPlaneNormal;
        [FieldOffset(84)] public float SolverWeight;
        [FieldOffset(88)] public float3 VisualOverkillOffset;
        [FieldOffset(100)] public float ReservedFloat0;
        [FieldOffset(104)] public ulong Reserved1;
        [FieldOffset(112)] public ulong Reserved2;
        [FieldOffset(120)] public ulong Reserved3;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Construction/Drone Bone Metadata")]
    public sealed class DroneBoneMetadata : MonoBehaviour
    {
        public const int RuntimeDataStrideBytes = 128;
        private const int RuntimeDataAlignmentBytes = 8;
        private static readonly Transform[] s_emptyBones = Array.Empty<Transform>();
        private static readonly DroneBoneJointDescriptor[] s_emptyJoints = Array.Empty<DroneBoneJointDescriptor>();

        [Header("Authoring Identity")]
        [SerializeField] private uint droneId;
        [SerializeField] private uint bakeHash;
        [SerializeField, Range(0f, 1f)] private float authoredQualityWeight = 1f;

        [Header("Rig Table")]
        [SerializeField] private Transform rigRoot;
        [SerializeField] private Transform[] bones = s_emptyBones;
        [SerializeField] private DroneBoneJointDescriptor[] joints = s_emptyJoints;

        public uint DroneId => droneId;
        public uint BakeHash => bakeHash;
        public float AuthoredQualityWeight => authoredQualityWeight;
        public Transform RigRoot => rigRoot;
        public int BoneCount => bones == null ? 0 : bones.Length;
        public int JointCount => joints == null ? 0 : joints.Length;

        public ReadOnlySpan<DroneBoneJointDescriptor> Joints =>
            joints == null ? ReadOnlySpan<DroneBoneJointDescriptor>.Empty : new ReadOnlySpan<DroneBoneJointDescriptor>(joints);

        public bool TryGetBoneTransform(int boneIndex, out Transform bone)
        {
            Transform[] source = bones;
            if (source == null || (uint)boneIndex >= (uint)source.Length)
            {
                bone = null;
                return false;
            }

            bone = source[boneIndex];
            return bone != null;
        }

        public bool TryGetJoint(int index, out DroneBoneJointDescriptor descriptor)
        {
            DroneBoneJointDescriptor[] source = joints;
            if (source == null || (uint)index >= (uint)source.Length)
            {
                descriptor = default;
                return false;
            }

            descriptor = source[index];
            return true;
        }

        public int CopyJointTableTo(NativeArray<DroneBoneJointRuntimeData> destination)
        {
            DroneBoneJointDescriptor[] source = joints;
            if (source == null || !destination.IsCreated)
                return 0;

            int count = source.Length < destination.Length ? source.Length : destination.Length;
            for (int i = 0; i < count; i++)
                destination[i] = ToRuntimeData(source[i]);

            return count;
        }

        public int CopyJointTableTo(DroneBoneJointRuntimeData[] destination)
        {
            DroneBoneJointDescriptor[] source = joints;
            if (source == null || destination == null)
                return 0;

            int count = source.Length < destination.Length ? source.Length : destination.Length;
            for (int i = 0; i < count; i++)
                destination[i] = ToRuntimeData(source[i]);

            return count;
        }

        public bool TryExportRuntimeJoint(int index, out DroneBoneJointRuntimeData runtimeData)
        {
            if (!TryGetJoint(index, out DroneBoneJointDescriptor descriptor))
            {
                runtimeData = default;
                return false;
            }

            runtimeData = ToRuntimeData(descriptor);
            return true;
        }

        public static bool ValidateStaticLayout()
        {
            int stride = UnsafeUtility.SizeOf<DroneBoneJointRuntimeData>();
            return stride == RuntimeDataStrideBytes && (stride & (RuntimeDataAlignmentBytes - 1)) == 0;
        }

        public static bool ValidateDescriptorSet(
            Transform[] boneRefs,
            DroneBoneJointDescriptor[] descriptors,
            out string failureReason)
        {
            if (!ValidateStaticLayout())
            {
                failureReason = "DroneBoneJointRuntimeData layout is invalid.";
                return false;
            }

            if (boneRefs == null || boneRefs.Length == 0)
            {
                failureReason = "DroneBoneMetadata has no bone refs.";
                return false;
            }

            if (descriptors == null || descriptors.Length == 0)
            {
                failureReason = "DroneBoneMetadata has no joint descriptors.";
                return false;
            }

            if (descriptors.Length > boneRefs.Length)
            {
                failureReason = "DroneBoneMetadata joint count exceeds bone ref count.";
                return false;
            }

            for (int i = 0; i < descriptors.Length; i++)
            {
                DroneBoneJointDescriptor descriptor = descriptors[i];
                if (descriptor.BoneIndex != i)
                {
                    failureReason = "DroneBoneMetadata joint table is not bone-index ordered.";
                    return false;
                }

                if ((uint)descriptor.BoneIndex >= (uint)boneRefs.Length ||
                    boneRefs[descriptor.BoneIndex] == null)
                {
                    failureReason = "DroneBoneMetadata bone index is invalid.";
                    return false;
                }

                if (descriptor.ParentIndex < -1 ||
                    descriptor.ParentIndex >= i ||
                    descriptor.ParentIndex >= boneRefs.Length ||
                    descriptor.ParentIndex == descriptor.BoneIndex ||
                    descriptor.BoneHash == 0u ||
                    !IsFinite(descriptor.BindLocalPosition) ||
                    !IsFinite(descriptor.LocalAxis) ||
                    !IsFinite(descriptor.LimitPlaneNormal) ||
                    !IsFinite(descriptor.VisualOverkillOffset) ||
                    !IsFinite(descriptor.BindLocalRotation) ||
                    !math.isfinite(descriptor.MinAngleDegrees) ||
                    !math.isfinite(descriptor.MaxAngleDegrees) ||
                    descriptor.MaxAngleDegrees < descriptor.MinAngleDegrees ||
                    !math.isfinite(descriptor.Stiffness) ||
                    !math.isfinite(descriptor.Damping) ||
                    !math.isfinite(descriptor.SolverWeight) ||
                    descriptor.Stiffness < 0f ||
                    descriptor.Damping < 0f ||
                    descriptor.SolverWeight < 0f ||
                    descriptor.SolverWeight > 1f ||
                    math.lengthsq((float3)(descriptor.LocalAxis)) <= 0.000001f ||
                    math.lengthsq((float3)(descriptor.LimitPlaneNormal)) <= 0.000001f)
                {
                    failureReason = "DroneBoneMetadata descriptor validation failed.";
                    return false;
                }

                for (int j = i + 1; j < descriptors.Length; j++)
                {
                    if (descriptors[j].BoneIndex == descriptor.BoneIndex ||
                        descriptors[j].BoneHash == descriptor.BoneHash)
                    {
                        failureReason = "DroneBoneMetadata duplicate bone index or hash.";
                        return false;
                    }
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            SanitizeSerializedState();
        }

        private void SanitizeSerializedState()
        {
            authoredQualityWeight = math.saturate(math.isfinite(authoredQualityWeight) ? authoredQualityWeight : 1f);
            if (bones == null)
                bones = s_emptyBones;
            if (joints == null)
                joints = s_emptyJoints;

            for (int i = 0; i < joints.Length; i++)
            {
                DroneBoneJointDescriptor descriptor = joints[i];
                descriptor.BoneIndex = math.max(0, descriptor.BoneIndex);
                descriptor.ParentIndex = descriptor.ParentIndex < 0 ? -1 : descriptor.ParentIndex;
                descriptor.TierMask = descriptor.TierMask == 0 ? DroneBoneTierMask.All : descriptor.TierMask;
                descriptor.SolverFlags = descriptor.SolverFlags == 0 ? DroneBoneSolverFlags.Active : descriptor.SolverFlags;
                descriptor.BindLocalPosition = SanitizeVector(descriptor.BindLocalPosition, Vector3.zero);
                descriptor.BindLocalRotation = SanitizeQuaternion(descriptor.BindLocalRotation);
                descriptor.LocalAxis = SanitizeDirection(descriptor.LocalAxis, Vector3.up);
                descriptor.LimitPlaneNormal = SanitizeDirection(descriptor.LimitPlaneNormal, Vector3.forward);
                descriptor.VisualOverkillOffset = SanitizeVector(descriptor.VisualOverkillOffset, Vector3.zero);
                descriptor.MinAngleDegrees = SanitizeFinite(descriptor.MinAngleDegrees, -45f);
                descriptor.MaxAngleDegrees = SanitizeFinite(descriptor.MaxAngleDegrees, 45f);
                if (descriptor.MaxAngleDegrees < descriptor.MinAngleDegrees)
                {
                    float min = descriptor.MaxAngleDegrees;
                    descriptor.MaxAngleDegrees = descriptor.MinAngleDegrees;
                    descriptor.MinAngleDegrees = min;
                }

                descriptor.Stiffness = math.max(0f, SanitizeFinite(descriptor.Stiffness, 1f));
                descriptor.Damping = math.max(0f, SanitizeFinite(descriptor.Damping, 0.25f));
                descriptor.SolverWeight = math.saturate(SanitizeFinite(descriptor.SolverWeight, 1f));
                joints[i] = descriptor;
            }
        }

        private static DroneBoneJointRuntimeData ToRuntimeData(DroneBoneJointDescriptor descriptor)
        {
            DroneBoneJointRuntimeData runtime = default;
            runtime.BoneIndex = descriptor.BoneIndex;
            runtime.ParentIndex = descriptor.ParentIndex;
            runtime.BoneHash = descriptor.BoneHash;
            runtime.SolverFlags = (byte)descriptor.SolverFlags;
            runtime.TierMask = (byte)descriptor.TierMask;
            runtime.BindLocalPosition = (float3)(descriptor.BindLocalPosition);
            runtime.BindLocalRotation = ToQuaternion(descriptor.BindLocalRotation);
            runtime.LocalAxis = math.normalizesafe((float3)(descriptor.LocalAxis), new float3(0f, 1f, 0f));
            runtime.LimitPlaneNormal = math.normalizesafe((float3)(descriptor.LimitPlaneNormal), new float3(0f, 0f, 1f));
            runtime.MinAngleDegrees = descriptor.MinAngleDegrees;
            runtime.MaxAngleDegrees = descriptor.MaxAngleDegrees;
            runtime.Stiffness = descriptor.Stiffness;
            runtime.Damping = descriptor.Damping;
            runtime.SolverWeight = descriptor.SolverWeight;
            runtime.VisualOverkillOffset = (float3)(descriptor.VisualOverkillOffset);
            return runtime;
        }

        private static quaternion ToQuaternion(Quaternion value)
        {
            return new quaternion(value.x, value.y, value.z, value.w);
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z) &&
                   math.isfinite(value.w) &&
                   math.lengthsq(new float4(value.x, value.y, value.z, value.w)) > 0.000001f;
        }

        private static Vector3 SanitizeVector(Vector3 value, Vector3 fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static Vector3 SanitizeDirection(Vector3 value, Vector3 fallback)
        {
            if (!IsFinite(value) || value.sqrMagnitude <= 0.000001f)
                return fallback;

            return value.normalized;
        }

        private static Quaternion SanitizeQuaternion(Quaternion value)
        {
            if (!IsFinite(value))
                return Quaternion.identity;

            return Quaternion.Normalize(value);
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

#if UNITY_EDITOR
        public void ConfigureEditorBake(
            uint authoredDroneId,
            uint authoredBakeHash,
            float globalQualityWeight,
            Transform authoredRigRoot,
            Transform[] authoredBones,
            DroneBoneJointDescriptor[] authoredJoints)
        {
            droneId = authoredDroneId;
            bakeHash = authoredBakeHash;
            authoredQualityWeight = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            rigRoot = authoredRigRoot;
            bones = authoredBones ?? s_emptyBones;
            joints = authoredJoints ?? s_emptyJoints;
            SanitizeSerializedState();
        }
#endif
    }
}
