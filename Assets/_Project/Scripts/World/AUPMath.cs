using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Shared deterministic AUP constants for terrain, KCC, and MapMagic handoff math.
    /// </summary>
    public static class AUPDeterminism
    {
        public const int AUP_DETERMINISM_MULTIPLIER = 1000;
    }

    /// <summary>
    /// Burst-safe Absolute Universe Position math shared by simulation systems.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal static class AUPMath
    {
        private const double CellSizeMeters = AbsoluteUniversePosition.CellSizeMeters;
        private const long DeltaClampCells = 1000000L;
        private static int _invalidResultCount;

        /// <summary>
        /// Computes squared distance between two Absolute Universe Positions without reducing precision to runtime space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double AUPDistanceSq(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            double3 delta = AUPDeltaClamped(in a, in b);
            return math.lengthsq(delta);
        }

        /// <summary>
        /// Computes grid-local meter delta with clamp rails for impossible sector separation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double3 AUPDeltaClamped(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            return new double3(
                AUPAxisDeltaClamped(a.GridX, b.GridX, a.LocalX, b.LocalX),
                AUPAxisDeltaClamped(a.GridY, b.GridY, a.LocalY, b.LocalY),
                AUPAxisDeltaClamped(a.GridZ, b.GridZ, a.LocalZ, b.LocalZ));
        }

        /// <summary>
        /// Computes a cheap max/mid/min AUP distance approximation using clamped double grid-local deltas.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double ApproximateAUPDistanceMetersClamped(in AbsoluteUniversePosition a, in AbsoluteUniversePosition b)
        {
            double3 delta = AUPDeltaClamped(in a, in b);
            double ax = math.abs(delta.x);
            double ay = math.abs(delta.y);
            double az = math.abs(delta.z);
            double max = math.max(ax, math.max(ay, az));
            double min = math.min(ax, math.min(ay, az));
            double mid = ax + ay + az - max - min;
            return max + (0.375d * mid) + (0.125d * min);
        }

        /// <summary>
        /// Computes a normalized direction from one Absolute Universe Position to another.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 AUPDirection(in AbsoluteUniversePosition from, in AbsoluteUniversePosition to)
        {
            double3 delta = AUPDeltaClamped(in to, in from);
            double distanceSq = math.lengthsq(delta);
            if (!math.isfinite(distanceSq) || distanceSq <= double.Epsilon)
                return float3.zero;

            double inverseLength = math.rsqrt(math.max(distanceSq, 0.0001d));
            double3 direction = delta * inverseLength;
            float3 result = new float3((float)direction.x, (float)direction.y, (float)direction.z);
            if (!math.all(math.isfinite(result)))
            {
                ReportInvalidFloatResult();
                return float3.zero;
            }

            return result;
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
        internal static float3 ToRuntimeFloat3(in AbsoluteUniversePosition position, double3 committedOffset)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 ToRuntimeFloat3(in AbsoluteUniversePosition position, float3 committedOffset)
        {
            return ToRuntimeFloat3(
                in position,
                new double3(committedOffset.x, committedOffset.y, committedOffset.z));
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
        /// Resolves an AUP plus a meter offset while keeping the offset in double precision.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double3 OffsetAbsoluteMeters(in AbsoluteUniversePosition position, double3 deltaMeters)
        {
            double3 absolute = ToAbsoluteDouble3(in position);
            if (!math.all(math.isfinite(deltaMeters)))
            {
                ReportInvalidFloatResult();
                return absolute;
            }

            double3 shifted = absolute + deltaMeters;
            if (!math.all(math.isfinite(shifted)))
            {
                ReportInvalidFloatResult();
                return absolute;
            }

            return shifted;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double3 WeightedAbsoluteAverage3(
            in AbsoluteUniversePosition a,
            in AbsoluteUniversePosition b,
            in AbsoluteUniversePosition c,
            double weight)
        {
            if (!math.isfinite(weight))
            {
                ReportInvalidFloatResult();
                weight = 1.0d / 3.0d;
            }

            double3 sum = ToAbsoluteDouble3(in a);
            sum += ToAbsoluteDouble3(in b);
            sum += ToAbsoluteDouble3(in c);
            double3 weighted = sum * weight;
            if (!math.all(math.isfinite(weighted)))
            {
                ReportInvalidFloatResult();
                return ToAbsoluteDouble3(in a);
            }

            return weighted;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double AUPAxisDeltaClamped(long aGrid, long bGrid, float aLocal, float bLocal)
        {
            if (aGrid > bGrid)
            {
                long positiveLimit = bGrid > long.MaxValue - DeltaClampCells
                    ? long.MaxValue
                    : bGrid + DeltaClampCells;
                if (aGrid > positiveLimit)
                    return double.MaxValue * 0.25d;
            }
            else if (aGrid < bGrid)
            {
                long negativeLimit = bGrid < long.MinValue + DeltaClampCells
                    ? long.MinValue
                    : bGrid - DeltaClampCells;
                if (aGrid < negativeLimit)
                    return double.MinValue * 0.25d;
            }

            long gridDelta = aGrid - bGrid;
            return (gridDelta * CellSizeMeters) + ((double)aLocal - bLocal);
        }

        [BurstDiscard]
        private static void ReportInvalidFloatResult()
        {
            Interlocked.Increment(ref _invalidResultCount);
        }
    }
}
