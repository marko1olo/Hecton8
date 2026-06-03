using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    [Serializable]
    [StructLayout(LayoutKind.Explicit, Size = SizeBytes)]
    public struct BreakerHandleData
    {
        public const int SizeBytes = 64;

        [FieldOffset(0)]
        public uint stableHash;
        [FieldOffset(4)]
        public int portIndex;
        [FieldOffset(8)]
        public float3 localPosition;
        [FieldOffset(20)]
        public float3 localForward;
        [FieldOffset(32)]
        public float3 localRotationAxis;
        [FieldOffset(44)]
        public float minAngleDegrees;
        [FieldOffset(48)]
        public float maxAngleDegrees;
        [FieldOffset(52)]
        public float gripRadiusMeters;
        [FieldOffset(56)]
        public uint flags;
        [FieldOffset(60)]
        public uint reserved0;

        public static bool ValidateBreakerHandleDataLayout()
        {
            int size = UnsafeUtility.SizeOf<BreakerHandleData>();
            return size == BreakerHandleData.SizeBytes && (size & 7) == 0;
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Power/Breaker Metadata")]
    public sealed class BreakerMetadata : MonoBehaviour
    {
        [SerializeField] private Transform primaryIkHandle;
        [SerializeField] private Transform[] ikHandles = Array.Empty<Transform>();
        [SerializeField] private BreakerHandleData[] handles = Array.Empty<BreakerHandleData>();
        [SerializeField] private bool defaultClosed = true;

        public Transform PrimaryIkHandle => primaryIkHandle;
        public int HandleCount => handles != null ? handles.Length : 0;
        public int HandleTransformCount => ikHandles != null ? ikHandles.Length : 0;
        public bool DefaultClosed => defaultClosed;

        public bool TryGetHandle(int index, out BreakerHandleData handle)
        {
            BreakerHandleData[] source = handles;
            if (source == null || (uint)index >= (uint)source.Length)
            {
                handle = default;
                return false;
            }

            handle = source[index];
            return true;
        }

        public bool TryGetHandleTransform(int index, out Transform handle)
        {
            Transform[] source = ikHandles;
            if (source == null || (uint)index >= (uint)source.Length)
            {
                handle = null;
                return false;
            }

            handle = source[index];
            return handle != null;
        }

#if UNITY_EDITOR
        public void ConfigureEditorBake(
            Transform ikHandle,
            Transform[] bakedIkHandles,
            BreakerHandleData[] bakedHandles,
            bool startsClosed)
        {
            primaryIkHandle = ikHandle;
            defaultClosed = startsClosed;
            ikHandles = bakedIkHandles != null && bakedIkHandles.Length > 0
                ? bakedIkHandles
                : Array.Empty<Transform>();
            handles = bakedHandles != null && bakedHandles.Length > 0
                ? bakedHandles
                : Array.Empty<BreakerHandleData>();
            SanitizeHandles();
        }
#endif

        private void SanitizeHandles()
        {
            if (handles == null)
            {
                handles = Array.Empty<BreakerHandleData>();
                return;
            }

            if (ikHandles == null)
                ikHandles = Array.Empty<Transform>();

            for (int i = 0; i < handles.Length; i++)
            {
                BreakerHandleData handle = handles[i];
                handle.localForward = NormalizeOrFallback(handle.localForward, new float3(0f, 0f, 1f));
                handle.localRotationAxis = NormalizeAxisOrFallback(handle.localRotationAxis, handle.localForward, new float3(0f, 1f, 0f));
                if (math.abs(handle.maxAngleDegrees - handle.minAngleDegrees) < 0.0001f)
                    handle.maxAngleDegrees = handle.minAngleDegrees + 90f;
                handle.gripRadiusMeters = math.max(0.01f, math.select(0.06f, handle.gripRadiusMeters, math.isfinite(handle.gripRadiusMeters)));
                handle.portIndex = math.max(0, handle.portIndex);
                handle.stableHash = handle.stableHash == 0u ? FallbackHandleHash(i) : handle.stableHash;
                handles[i] = handle;
            }
        }

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static float3 NormalizeAxisOrFallback(float3 axis, float3 forward, float3 fallback)
        {
            float3 safeForward = NormalizeOrFallback(forward, new float3(0f, 0f, 1f));
            float3 safeAxis = NormalizeOrFallback(axis, fallback);
            if (math.abs(math.dot(safeAxis, safeForward)) <= 0.95f)
                return safeAxis;

            float3 candidate = math.abs(safeForward.y) < 0.75f
                ? new float3(0f, 1f, 0f)
                : new float3(1f, 0f, 0f);
            float3 projected = candidate - safeForward * math.dot(candidate, safeForward);
            return NormalizeOrFallback(projected, fallback);
        }

        private static uint FallbackHandleHash(int index)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash ^= (uint)(index + 1);
                hash *= 16777619u;
                return hash == 0u ? 2166136261u : hash;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SanitizeHandles();
        }
#endif
    }
}
