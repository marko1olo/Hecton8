using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Signed 64-bit integer vector for quantized AUP millimeter hashes.
    /// Unity.Mathematics in this project does not provide long3.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct long3
    {
        [FieldOffset(0)] public long x;
        [FieldOffset(8)] public long y;
        [FieldOffset(16)] public long z;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long3(long x, long y, long z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    /// <summary>
    /// Shared AUP precision constants and Burst-safe double-to-local helpers.
    /// </summary>
    public static class AupPrecisionMath
    {
        public const int TelemetryCapacity = 300;
        public const float DefaultNormalizeEpsilonSq = 0.000001f;
        public const float DefaultMaxLocalCastMeters = 131072f;
        public const float DefaultGateMinMeters = 1000f;
        public const float DefaultGateMaxMeters = 5000f;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 CreateOutOfBoundsSentinel()
        {
            return new float3(DefaultMaxLocalCastMeters, DefaultMaxLocalCastMeters, DefaultMaxLocalCastMeters);
        }

        private const ulong PackedSectorHashMarker = 0x8000000000000000UL;
        private const int PackedSectorAxisBits = 21;
        private const int PackedSectorAxisBias = 1 << (PackedSectorAxisBits - 1);
        private const int PackedSectorAxisMask = (1 << PackedSectorAxisBits) - 1;
        private const double MillimetersPerMeter = 1000.0d;

        /// <summary>
        /// Computes local delta in double precision. This is the only approved first step before float downcast.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 LocalDeltaDouble(double3 targetAup, double3 observerAup)
        {
            return targetAup - observerAup;
        }

        /// <summary>
        /// Subtracts observer AUP in double precision before downcasting the local delta to float.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 LocalDeltaFloat3(double3 targetAup, double3 observerAup, float3 fallback)
        {
            return DowncastLocalDelta(LocalDeltaDouble(targetAup, observerAup), fallback);
        }

        /// <summary>
        /// Subtracts observer AUP in double precision, clamps only the already-local delta, then downcasts to float.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 LocalDeltaFloat3Clamped(double3 targetAup, double3 observerAup, float maxLocalMeters, float3 fallback)
        {
            return DowncastLocalDeltaClamped(LocalDeltaDouble(targetAup, observerAup), maxLocalMeters, fallback);
        }

        /// <summary>
        /// Downcasts an already-local double delta. Do not pass absolute coordinates here.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 DowncastLocalDelta(double3 localDelta, float3 fallback)
        {
            if (!math.all(math.isfinite(localDelta)))
                return fallback;

            float3 result = new float3((float)localDelta.x, (float)localDelta.y, (float)localDelta.z);
            return math.all(math.isfinite(result)) ? result : fallback;
        }

        /// <summary>
        /// Downcasts a double-domain procedural phase after all scale/subtract work is complete.
        /// This is not a spatial authority conversion.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 DowncastProceduralPhase(double3 phase, float3 fallback)
        {
            if (!math.all(math.isfinite(phase)))
                return fallback;

            float3 result = new float3((float)phase.x, (float)phase.y, (float)phase.z);
            return math.all(math.isfinite(result)) ? result : fallback;
        }

        /// <summary>
        /// Downcasts an already-local double delta with physics/rendering guard rails.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 DowncastLocalDeltaClamped(double3 localDelta, float maxLocalMeters, float3 fallback)
        {
            if (!math.all(math.isfinite(localDelta)))
                return fallback;

            double safeMax = math.max((double)maxLocalMeters, 1.0d);
            double3 clamped = math.clamp(localDelta, new double3(-safeMax), new double3(safeMax));
            return DowncastLocalDelta(clamped, fallback);
        }

        /// <summary>
        /// Squared distance in double precision. Never demotes operands before multiplication.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double DistanceSqSafeDouble(double3 targetAup, double3 observerAup)
        {
            double3 localDelta = LocalDeltaDouble(targetAup, observerAup);
            double distanceSq = math.lengthsq(localDelta);
            return math.isfinite(distanceSq) && distanceSq >= 0.0d ? distanceSq : double.MaxValue;
        }

        /// <summary>
        /// Squared distance as float after double-precision squaring.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceSqSafeFloat(double3 targetAup, double3 observerAup)
        {
            double distanceSq = DistanceSqSafeDouble(targetAup, observerAup);
            if (distanceSq >= float.MaxValue)
                return float.MaxValue;
            return math.isfinite(distanceSq) ? (float)distanceSq : float.MaxValue;
        }

        /// <summary>
        /// Safe float normalization. Returns fallback for non-finite or near-zero vectors.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SafeNormalize(float3 value, float3 fallback, float epsilonSq = DefaultNormalizeEpsilonSq)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= math.max(epsilonSq, 1e-12f))
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        /// <summary>
        /// Subtracts in double, downcasts local delta, and returns a NaN-safe normalized direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SafeNormalizeLocalDelta(double3 targetAup, double3 observerAup, float3 fallback, float epsilonSq = DefaultNormalizeEpsilonSq)
        {
            float3 local = LocalDeltaFloat3(targetAup, observerAup, fallback);
            return SafeNormalize(local, fallback, epsilonSq);
        }

        /// <summary>
        /// Continuous quality distance gate. Precision remains fixed; quality changes how many far entities are localized.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveGateDistanceMeters(float globalQualityWeight, float minMeters = DefaultGateMinMeters, float maxMeters = DefaultGateMaxMeters)
        {
            float q = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            float minSafe = math.max(1f, math.select(DefaultGateMinMeters, minMeters, math.isfinite(minMeters)));
            float maxSafe = math.max(minSafe, math.select(DefaultGateMaxMeters, maxMeters, math.isfinite(maxMeters)));
            return math.lerp(minSafe, maxSafe, q);
        }

        /// <summary>
        /// Returns true when the target is outside the continuous localization gate.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldSkipByDistanceSq(double distanceSq, float globalQualityWeight, float minMeters, float maxMeters)
        {
            float gate = ResolveGateDistanceMeters(globalQualityWeight, minMeters, maxMeters);
            double gateSq = (double)gate * gate;
            return !math.isfinite(distanceSq) || distanceSq > gateSq;
        }

        /// <summary>
        /// Packs signed sector coordinates into a reversible 64-bit hash for deterministic center reconstruction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong PackDeterministicSectorHash(int3 sector)
        {
            uint x = PackSectorAxis(sector.x);
            uint y = PackSectorAxis(sector.y);
            uint z = PackSectorAxis(sector.z);
            return PackedSectorHashMarker |
                   x |
                   ((ulong)y << PackedSectorAxisBits) |
                   ((ulong)z << (PackedSectorAxisBits * 2));
        }

        /// <summary>
        /// Reconstructs the exact center of a reversibly encoded sector hash.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryConvertSectorHashToAup(ulong sectorHash, double sectorSizeMeters, out double3 centerAup)
        {
            centerAup = double3.zero;
            if ((sectorHash & PackedSectorHashMarker) == 0UL)
                return false;

            int3 sector = UnpackDeterministicSectorHash(sectorHash);
            double size = math.max(sectorSizeMeters, 1.0d);
            centerAup = (new double3(sector.x, sector.y, sector.z) + new double3(0.5d)) * size;
            return math.all(math.isfinite(centerAup));
        }

        /// <summary>
        /// Decodes a hash produced by <see cref="PackDeterministicSectorHash"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 UnpackDeterministicSectorHash(ulong sectorHash)
        {
            int x = UnpackSectorAxis((uint)(sectorHash & PackedSectorAxisMask));
            int y = UnpackSectorAxis((uint)((sectorHash >> PackedSectorAxisBits) & PackedSectorAxisMask));
            int z = UnpackSectorAxis((uint)((sectorHash >> (PackedSectorAxisBits * 2)) & PackedSectorAxisMask));
            return new int3(x, y, z);
        }

        /// <summary>
        /// Returns a deterministic FNV-1a hash for ASCII byte spans.
        /// </summary>
        public static uint HashFnv1A32(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
                hash = unchecked((hash ^ value[i]) * 16777619u);
            return hash == 0u ? 1u : hash;
        }

        /// <summary>
        /// Allocation-free invariant float parser for cold CSV ingestion.
        /// </summary>
        public static bool TryParseFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            if (bytes.Length == 0)
                return false;

            int i = 0;
            bool negative = false;
            if (bytes[i] == (byte)'-' || bytes[i] == (byte)'+')
            {
                negative = bytes[i] == (byte)'-';
                i++;
            }

            double result = 0d;
            bool hasDigit = false;
            for (; i < bytes.Length; i++)
            {
                byte c = bytes[i];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                hasDigit = true;
                result = (result * 10d) + (c - (byte)'0');
            }

            if (i < bytes.Length && bytes[i] == (byte)'.')
            {
                i++;
                double scale = 0.1d;
                for (; i < bytes.Length; i++)
                {
                    byte c = bytes[i];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;
                    hasDigit = true;
                    result += (c - (byte)'0') * scale;
                    scale *= 0.1d;
                }
            }

            if (!hasDigit)
                return false;

            if (i < bytes.Length && (bytes[i] == (byte)'e' || bytes[i] == (byte)'E'))
            {
                i++;
                bool exponentNegative = false;
                if (i < bytes.Length && (bytes[i] == (byte)'-' || bytes[i] == (byte)'+'))
                {
                    exponentNegative = bytes[i] == (byte)'-';
                    i++;
                }

                int exponent = 0;
                bool hasExponentDigit = false;
                for (; i < bytes.Length; i++)
                {
                    byte c = bytes[i];
                    if (c < (byte)'0' || c > (byte)'9')
                        return false;
                    hasExponentDigit = true;
                    exponent = (exponent * 10) + (c - (byte)'0');
                    if (exponent > 38)
                        exponent = 38;
                }

                if (!hasExponentDigit)
                    return false;

                double factor = Pow10(exponent);
                result = exponentNegative ? result / factor : result * factor;
            }
            else if (i != bytes.Length)
                return false;

            if (negative)
                result = -result;

            if (!math.isfinite(result) || result > float.MaxValue || result < -float.MaxValue)
                return false;

            value = (float)result;
            return math.isfinite(value);
        }

        /// <summary>
        /// Parses a cold CSV row: subsystem,normalize_epsilon_sq,gate_min,gate_max,max_local,warn_local.
        /// </summary>
        public static bool TryParseToleranceProfileRow(ReadOnlySpan<byte> row, out AupToleranceProfileDTO profile)
        {
            profile = default;
            if (row.Length == 0 || row[0] == (byte)'#')
                return false;

            ReadOnlySpan<byte> token0 = NextToken(row, 0, out int next);
            if (token0.Length == 0 || EqualsAscii(token0, "subsystem"))
                return false;

            profile.SubsystemHash = HashFnv1A32(token0);
            if (!TryParseFloat(NextToken(row, next, out next), out profile.NormalizeEpsilonSq))
                return false;
            if (!TryParseFloat(NextToken(row, next, out next), out profile.GateMinMeters))
                return false;
            if (!TryParseFloat(NextToken(row, next, out next), out profile.GateMaxMeters))
                return false;
            if (!TryParseFloat(NextToken(row, next, out next), out profile.MaxLocalCastMeters))
                return false;
            if (!TryParseFloat(NextToken(row, next, out _), out profile.WarningLocalMeters))
                return false;

            profile.NormalizeEpsilonSq = math.max(1e-12f, profile.NormalizeEpsilonSq);
            profile.GateMinMeters = math.max(1f, profile.GateMinMeters);
            profile.GateMaxMeters = math.max(profile.GateMinMeters, profile.GateMaxMeters);
            profile.MaxLocalCastMeters = math.max(1f, profile.MaxLocalCastMeters);
            profile.WarningLocalMeters = math.max(1f, profile.WarningLocalMeters);
            profile.Flags = 1u;
            return true;
        }

        /// <summary>
        /// Quantizes an absolute position to millimeters for deterministic hashing and rollback comparisons.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long3 QuantizeMillimeters(double3 aup)
        {
            return new long3(
                ClampDoubleToLong(math.round(aup.x * MillimetersPerMeter)),
                ClampDoubleToLong(math.round(aup.y * MillimetersPerMeter)),
                ClampDoubleToLong(math.round(aup.z * MillimetersPerMeter)));
        }

        /// <summary>
        /// Hashes a double3 after millimeter quantization.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong HashQuantizedAup(double3 aup)
        {
            long3 mm = QuantizeMillimeters(aup);
            ulong hash = 14695981039346656037UL;
            hash = MixLong(hash, mm.x);
            hash = MixLong(hash, mm.y);
            hash = MixLong(hash, mm.z);
            return hash == 0UL ? 1UL : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint PackSectorAxis(int value)
        {
            int clamped = math.clamp(value, -PackedSectorAxisBias, PackedSectorAxisBias - 1);
            return (uint)(clamped + PackedSectorAxisBias);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int UnpackSectorAxis(uint value)
        {
            return (int)(value & PackedSectorAxisMask) - PackedSectorAxisBias;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ClampDoubleToLong(double value)
        {
            if (!math.isfinite(value))
                return 0L;
            if (value >= long.MaxValue)
                return long.MaxValue;
            if (value <= long.MinValue)
                return long.MinValue;
            return (long)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong MixLong(ulong hash, long value)
        {
            ulong v = unchecked((ulong)value);
            for (int shift = 0; shift < 64; shift += 8)
                hash = (hash ^ ((v >> shift) & 0xFFUL)) * 1099511628211UL;
            return hash;
        }

        private static double Pow10(int exponent)
        {
            double value = 1d;
            for (int i = 0; i < exponent; i++)
                value *= 10d;
            return value;
        }

        private static ReadOnlySpan<byte> NextToken(ReadOnlySpan<byte> row, int start, out int next)
        {
            int begin = math.clamp(start, 0, row.Length);
            while (begin < row.Length && (row[begin] == (byte)' ' || row[begin] == (byte)'\t'))
                begin++;

            int end = begin;
            while (end < row.Length && row[end] != (byte)',')
                end++;

            int trimmedEnd = end;
            while (trimmedEnd > begin && (row[trimmedEnd - 1] == (byte)' ' || row[trimmedEnd - 1] == (byte)'\t' || row[trimmedEnd - 1] == (byte)'\r'))
                trimmedEnd--;

            next = end < row.Length ? end + 1 : row.Length;
            return row.Slice(begin, trimmedEnd - begin);
        }

        private static bool EqualsAscii(ReadOnlySpan<byte> bytes, string ascii)
        {
            if (bytes.Length != ascii.Length)
                return false;

            for (int i = 0; i < bytes.Length; i++)
            {
                byte a = bytes[i];
                byte b = (byte)ascii[i];
                if (a >= (byte)'A' && a <= (byte)'Z')
                    a = (byte)(a + 32);
                if (a != b)
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Cold-boot AUP tolerance profile parsed from CSV without managed string splitting.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AupToleranceProfileDTO
    {
        [FieldOffset(0)] public uint SubsystemHash;
        [FieldOffset(4)] public float NormalizeEpsilonSq;
        [FieldOffset(8)] public float GateMinMeters;
        [FieldOffset(12)] public float GateMaxMeters;
        [FieldOffset(16)] public float MaxLocalCastMeters;
        [FieldOffset(20)] public float WarningLocalMeters;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
        [FieldOffset(32)] public ulong _pad1;
        [FieldOffset(40)] public ulong _pad2;
        [FieldOffset(48)] public ulong _pad3;
        [FieldOffset(56)] public ulong _pad4;
    }

    /// <summary>
    /// 64-byte AUP precision black-box row for the last 300 localization frames.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AupPrecisionTelemetryEntry
    {
        [FieldOffset(0)] public double MaxLocalDistanceMeters;
        [FieldOffset(8)] public double MaxLocalDistanceSq;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint ActiveCount;
        [FieldOffset(24)] public uint SkippedCount;
        [FieldOffset(28)] public uint NonFiniteCount;
        [FieldOffset(32)] public uint SafeNormalizeFallbackCount;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public float KernelMicrosecondsEstimate;
        [FieldOffset(44)] public float GateDistanceMeters;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint SectorHash;
        [FieldOffset(56)] public ulong PositionHash;
    }

    /// <summary>
    /// Owner-local AUP precision runtime control row. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AupPrecisionRuntimeStateDTO
    {
        [FieldOffset(0)] public double3 ObserverAup;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public int ActiveCount;
        [FieldOffset(32)] public int TelemetryCursor;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public float GateDistanceMeters;
        [FieldOffset(44)] public float MaxLocalCastMeters;
        [FieldOffset(48)] public float LastKernelMicroseconds;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] private ulong _pad0;
    }

    /// <summary>
    /// Cache-line isolated precision fault counters. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AupPrecisionFaultCounter64
    {
        [FieldOffset(0)] public int NonFiniteCount;
        [FieldOffset(4)] public int ClampedCount;
        [FieldOffset(8)] public int SkippedCount;
        [FieldOffset(12)] public int SafeNormalizeFallbackCount;
        [FieldOffset(16)] public float MaxErrorMeters;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ulong PositionHash;
        [FieldOffset(32)] private ulong _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }
}
