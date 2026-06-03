using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct ValveHandleKinematicDTO
    {
        [FieldOffset(0)]
        public Vector3 LocalPivot;
        [FieldOffset(12)]
        public Vector3 LocalAxis;
        [FieldOffset(24)]
        public float MinAngleDegrees;
        [FieldOffset(28)]
        public float MaxAngleDegrees;
        [FieldOffset(32)]
        public uint HandleHash;
        [FieldOffset(36)]
        private uint _pad0;
    }

    [Serializable]
    public struct ValveHandleDescriptor
    {
        public Transform IKHandle;
        public Transform WheelVisual;
        public ValveHandleKinematicDTO Kinematics;

        public Vector3 LocalPivot
        {
            get => Kinematics.LocalPivot;
            set => Kinematics.LocalPivot = value;
        }

        public Vector3 LocalAxis
        {
            get => Kinematics.LocalAxis;
            set => Kinematics.LocalAxis = value;
        }

        public float MinAngleDegrees
        {
            get => Kinematics.MinAngleDegrees;
            set => Kinematics.MinAngleDegrees = value;
        }

        public float MaxAngleDegrees
        {
            get => Kinematics.MaxAngleDegrees;
            set => Kinematics.MaxAngleDegrees = value;
        }

        public uint HandleHash
        {
            get => Kinematics.HandleHash;
            set => Kinematics.HandleHash = value;
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Construction/Valve Metadata")]
    public sealed class ValveMetadata : MonoBehaviour
    {
        private static readonly ValveHandleDescriptor[] s_emptyHandles = Array.Empty<ValveHandleDescriptor>();

        [SerializeField] private ValveHandleDescriptor[] handles = s_emptyHandles;

        public int HandleCount => handles != null ? handles.Length : 0;

        public bool TryGetHandle(int index, out ValveHandleDescriptor descriptor)
        {
            ValveHandleDescriptor[] source = handles;
            if (source == null || (uint)index >= (uint)source.Length)
            {
                descriptor = default;
                return false;
            }

            descriptor = source[index];
            return true;
        }

        public bool TryGetHandleKinematics(int index, out ValveHandleKinematicDTO kinematics)
        {
            kinematics = default;
            if (!TryGetHandle(index, out ValveHandleDescriptor descriptor) ||
                !IsValidHandleForBake(in descriptor))
            {
                return false;
            }

            kinematics = descriptor.Kinematics;
            return true;
        }

        public bool ValidateHandlesForBake()
        {
            ValveHandleDescriptor[] source = handles;
            if (source == null || source.Length == 0)
                return false;

            for (int i = 0; i < source.Length; i++)
            {
                if (!IsValidHandleForBake(in source[i]))
                    return false;
            }

            return true;
        }

        private void OnValidate()
        {
            SanitizeSerializedState();
        }

        private void SanitizeSerializedState()
        {
            if (handles == null)
                return;

            for (int i = 0; i < handles.Length; i++)
            {
                ValveHandleDescriptor handle = handles[i];
                if (!IsFinite(handle.Kinematics.LocalPivot))
                    handle.Kinematics.LocalPivot = Vector3.zero;
                handle.Kinematics.LocalAxis = NormalizeDirection(handle.Kinematics.LocalAxis, Vector3.forward);

                if (!math.isfinite(handle.Kinematics.MinAngleDegrees))
                    handle.Kinematics.MinAngleDegrees = 0f;
                if (!math.isfinite(handle.Kinematics.MaxAngleDegrees))
                    handle.Kinematics.MaxAngleDegrees = 90f;
                if (handle.Kinematics.MaxAngleDegrees < handle.Kinematics.MinAngleDegrees + 1f)
                    handle.Kinematics.MaxAngleDegrees = handle.Kinematics.MinAngleDegrees + 1f;

                handles[i] = handle;
            }
        }

        private static bool IsValidHandleForBake(in ValveHandleDescriptor handle)
        {
            if (handle.IKHandle == null ||
                !IsFinite(handle.Kinematics.LocalPivot) ||
                !IsFinite(handle.Kinematics.LocalAxis) ||
                !math.isfinite(handle.Kinematics.MinAngleDegrees) ||
                !math.isfinite(handle.Kinematics.MaxAngleDegrees) ||
                handle.Kinematics.MaxAngleDegrees < handle.Kinematics.MinAngleDegrees + 1f)
            {
                return false;
            }

            float3 axis = new float3(handle.Kinematics.LocalAxis.x, handle.Kinematics.LocalAxis.y, handle.Kinematics.LocalAxis.z);
            float lengthSq = math.lengthsq(axis);
            return lengthSq >= 0.999f && lengthSq <= 1.001f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private static Vector3 NormalizeDirection(Vector3 value, Vector3 fallback)
        {
            float3 direction = new float3(value.x, value.y, value.z);
            if (!math.all(math.isfinite(direction)) || math.lengthsq(direction) <= 0.000001f)
                return fallback;

            float3 normalized = math.normalize(direction);
            return new Vector3(normalized.x, normalized.y, normalized.z);
        }

        public static bool ValidateUnmanagedLayout(out int handleBytes)
        {
            handleBytes = UnsafeUtility.SizeOf<ValveHandleKinematicDTO>();
            return handleBytes == 40 &&
                   (handleBytes & 7) == 0 &&
                   OffsetOf<ValveHandleKinematicDTO>(nameof(ValveHandleKinematicDTO.LocalPivot)) == 0 &&
                   OffsetOf<ValveHandleKinematicDTO>(nameof(ValveHandleKinematicDTO.LocalAxis)) == 12 &&
                   OffsetOf<ValveHandleKinematicDTO>(nameof(ValveHandleKinematicDTO.MinAngleDegrees)) == 24 &&
                   OffsetOf<ValveHandleKinematicDTO>(nameof(ValveHandleKinematicDTO.MaxAngleDegrees)) == 28 &&
                   OffsetOf<ValveHandleKinematicDTO>(nameof(ValveHandleKinematicDTO.HandleHash)) == 32;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }

#if UNITY_EDITOR
        public void ConfigureEditorBake(ValveHandleDescriptor[] bakedHandles)
        {
            handles = bakedHandles != null ? bakedHandles : s_emptyHandles;
            SanitizeSerializedState();
        }
#endif
    }
}
