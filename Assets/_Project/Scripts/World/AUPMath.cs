using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Burst-safe Absolute Universe Position math shared by simulation systems.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal static class AUPMath
    {
        private const double CellSizeMeters = AbsoluteUniversePosition.CellSizeMeters;
        private static int _invalidResultCount;

        /// <summary>
        /// Computes squared distance between two Absolute Universe Positions without reducing precision to runtime space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double AUPDistanceSq(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            double3 delta = AUPDelta(in a, in b);
            return math.dot(delta, delta);
        }

        /// <summary>
        /// Computes a normalized direction from one Absolute Universe Position to another.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 AUPDirection(in AbsoluteUniversePosition from, in AbsoluteUniversePosition to)
        {
            double3 delta = AUPDelta(in to, in from);
            return math.normalizesafe(new float3((float)delta.x, (float)delta.y, (float)delta.z), float3.zero);
        }

        /// <summary>
        /// Converts an Absolute Universe Position into camera-relative render space using long-sector delta math.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 ResolveCameraRelative(in AbsoluteUniversePosition target, in AbsoluteUniversePosition camera)
        {
            double3 delta = AUPDelta(in target, in camera);
            float3 result = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            if (!math.all(math.isfinite(result)))
                ReportInvalidFloatResult();

            return result;
        }

        /// <summary>
        /// Converts an Absolute Universe Position into local view space relative to another AUP origin.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 AUPToLocalViewSpace(in AbsoluteUniversePosition point, in AbsoluteUniversePosition viewOrigin)
        {
            return ResolveCameraRelative(in point, in viewOrigin);
        }

        /// <summary>
        /// Converts an Absolute Universe Position into runtime presentation space after committed origin offset.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 ToRuntimeFloat3(in AbsoluteUniversePosition position, float3 committedOffset)
        {
            double3 absolute = ToAbsoluteDouble3(in position);
            float3 result = new float3(
                (float)(absolute.x - committedOffset.x),
                (float)(absolute.y - committedOffset.y),
                (float)(absolute.z - committedOffset.z));
            if (!math.all(math.isfinite(result)))
                ReportInvalidFloatResult();

            return result;
        }

        /// <summary>
        /// Resolves the absolute double-precision coordinate for an AUP value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double3 ToAbsoluteDouble3(in AbsoluteUniversePosition position)
        {
            return new double3(
                (position.GridX * CellSizeMeters) + position.LocalX,
                (position.GridY * CellSizeMeters) + position.LocalY,
                (position.GridZ * CellSizeMeters) + position.LocalZ);
        }

        /// <summary>
        /// Drains invalid downcast detections for the fixed-step NaN Inquisitor.
        /// </summary>
        internal static int ConsumeInvalidResultCount()
        {
            return Interlocked.Exchange(ref _invalidResultCount, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 AUPDelta(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            long gridDeltaX = a.GridX - b.GridX;
            long gridDeltaY = a.GridY - b.GridY;
            long gridDeltaZ = a.GridZ - b.GridZ;
            return new double3(
                (gridDeltaX * CellSizeMeters) + ((double)a.LocalX - b.LocalX),
                (gridDeltaY * CellSizeMeters) + ((double)a.LocalY - b.LocalY),
                (gridDeltaZ * CellSizeMeters) + ((double)a.LocalZ - b.LocalZ));
        }

        [BurstDiscard]
        private static void ReportInvalidFloatResult()
        {
            Interlocked.Increment(ref _invalidResultCount);
        }
    }
}
