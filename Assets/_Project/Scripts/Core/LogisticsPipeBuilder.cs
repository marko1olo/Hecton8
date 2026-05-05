using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core
{
    [Flags]
    internal enum PipeRenderFlags : byte
    {
        None = 0,
        MaskRuptured = 1 << 0
    }

    internal enum PipeVisualLod : byte
    {
        Tube8 = 0,
        Tube4 = 1,
        Line = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SplineDescriptor
    {
        public float3 Start;
        public float3 End;
        public float3 StartForward;
        public float3 EndForward;
        public float Radius;
        public float RuptureStartTimeSeconds;
        public PipeRenderFlags Flags;
    }

    /// <summary>
    /// Authoritative spline math for logistics pipes. Uses a rotation-minimizing frame so 90-degree
    /// turns and zero-curvature spans do not twist like Frenet frames.
    /// </summary>
    internal static class LogisticsPipeBuilder
    {
        internal const float DefaultPipeRadiusMeters = 0.06f;
        internal const float UnsupportedSpanMeters = 15f;
        internal const float FarPipeLodMeters = 40f;
        internal const float LinePipeLodMeters = 100f;

        internal static SplineDescriptor CreateLinearDescriptor(float3 start, float3 end, float radius, PipeRenderFlags flags)
        {
            float3 chordDirection = SafeNormalize(end - start, new float3(0f, 0f, 1f));
            return new SplineDescriptor
            {
                Start = start,
                End = end,
                StartForward = chordDirection,
                EndForward = -chordDirection,
                Radius = math.max(0.001f, radius),
                RuptureStartTimeSeconds = 0f,
                Flags = flags
            };
        }

        internal static SplineDescriptor CreateSocketDescriptor(
            float3 start,
            float3 end,
            float3 startForward,
            float3 endForward,
            float radius,
            PipeRenderFlags flags)
        {
            float3 chordDirection = SafeNormalize(end - start, new float3(0f, 0f, 1f));
            return new SplineDescriptor
            {
                Start = start,
                End = end,
                StartForward = SafeNormalize(startForward, chordDirection),
                EndForward = SafeNormalize(endForward, -chordDirection),
                Radius = math.max(0.001f, radius),
                RuptureStartTimeSeconds = 0f,
                Flags = flags
            };
        }

        internal static PipeVisualLod ResolveVisualLod(in SplineDescriptor descriptor, float3 observerPosition)
        {
            float3 midpoint = (descriptor.Start + descriptor.End) * 0.5f;
            float distanceSq = math.lengthsq(midpoint - observerPosition);
            if (distanceSq > LinePipeLodMeters * LinePipeLodMeters)
                return PipeVisualLod.Line;

            if (distanceSq > FarPipeLodMeters * FarPipeLodMeters)
                return PipeVisualLod.Tube4;

            return PipeVisualLod.Tube8;
        }

        internal static void ResolveControlPoints(in SplineDescriptor descriptor, out float3 p0, out float3 p1, out float3 p2, out float3 p3)
        {
            float3 chord = descriptor.End - descriptor.Start;
            float distance = math.length(chord);
            float3 chordDirection = SafeNormalize(chord, new float3(0f, 0f, 1f));
            float3 startForward = SafeNormalize(descriptor.StartForward, chordDirection);
            float3 endForward = SafeNormalize(descriptor.EndForward, -chordDirection);

            float handleDistance = math.min(distance * 0.35f, 4.5f);
            if (handleDistance < 0.05f)
                handleDistance = distance * 0.25f;

            p0 = descriptor.Start;
            p1 = descriptor.Start + startForward * handleDistance;
            p2 = descriptor.End - endForward * handleDistance;
            p3 = descriptor.End;
        }

        internal static float3 EvaluateSpline(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float omt = 1f - t;
            float omt2 = omt * omt;
            float omt3 = omt2 * omt;
            float t2 = t * t;
            float t3 = t2 * t;
            return (omt3 * p0) +
                   (3f * omt2 * t * p1) +
                   (3f * omt * t2 * p2) +
                   (t3 * p3);
        }

        internal static float3 EvaluateTangent(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float omt = 1f - t;
            float omt2 = omt * omt;
            float t2 = t * t;
            return (3f * omt2 * (p1 - p0)) +
                   (6f * omt * t * (p2 - p1)) +
                   (3f * t2 * (p3 - p2));
        }

        internal static void ResolveInitialFrame(float3 tangent, out float3 normal, out float3 binormal)
        {
            float3 referenceUp = math.abs(tangent.y) > 0.98f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
            normal = SafeNormalize(referenceUp - tangent * math.dot(referenceUp, tangent), new float3(0f, 0f, 1f));
            binormal = SafeNormalize(math.cross(tangent, normal), new float3(1f, 0f, 0f));
            normal = SafeNormalize(math.cross(binormal, tangent), new float3(0f, 1f, 0f));
        }

        internal static void TransportFrame(
            float3 previousTangent,
            float3 currentTangent,
            float3 previousNormal,
            float3 previousBinormal,
            out float3 normal,
            out float3 binormal)
        {
            float3 rotationAxis = math.cross(previousTangent, currentTangent);
            float axisLengthSq = math.lengthsq(rotationAxis);
            float tangentDot = math.clamp(math.dot(previousTangent, currentTangent), -1f, 1f);

            if (axisLengthSq <= 0.000001f)
            {
                if (tangentDot < -0.9999f)
                {
                    float3 fallbackAxis = math.abs(previousTangent.y) > 0.98f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
                    rotationAxis = SafeNormalize(math.cross(previousTangent, fallbackAxis), new float3(0f, 0f, 1f));
                    normal = RotateAroundAxis(previousNormal, rotationAxis, -1f, 0f);
                    binormal = SafeNormalize(math.cross(currentTangent, normal), previousBinormal);
                    normal = SafeNormalize(math.cross(binormal, currentTangent), previousNormal);
                    return;
                }

                normal = previousNormal;
                binormal = previousBinormal;
                return;
            }

            float axisLength = math.sqrt(axisLengthSq);
            float3 axis = rotationAxis / axisLength;
            normal = RotateAroundAxis(previousNormal, axis, tangentDot, axisLength);
            binormal = RotateAroundAxis(previousBinormal, axis, tangentDot, axisLength);
            normal = SafeNormalize(normal - currentTangent * math.dot(normal, currentTangent), previousNormal);
            binormal = SafeNormalize(math.cross(currentTangent, normal), previousBinormal);
            normal = SafeNormalize(math.cross(binormal, currentTangent), previousNormal);
        }

        internal static bool HasRupturedMask(PipeRenderFlags flags)
        {
            return (flags & PipeRenderFlags.MaskRuptured) != 0;
        }

        internal static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (lengthSq <= 0.000001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static float3 RotateAroundAxis(float3 vector, float3 axis, float cosTheta, float sinTheta)
        {
            return vector * cosTheta +
                   math.cross(axis, vector) * sinTheta +
                   axis * math.dot(axis, vector) * (1f - cosTheta);
        }
    }
}
