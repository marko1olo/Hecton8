using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    public static class SpawnZoneSdfValidationConstants
    {
        public const int RequestSizeBytes = 32;
        public const int GridHeaderSizeBytes = 64;
        public const int TuningSizeBytes = 32;
        public const int RingStateSizeBytes = 64;
        public const int TelemetrySizeBytes = 64;
        public const int ProfileSizeBytes = 32;
        public const int DefaultRequestCapacity = 1024;
        public const int DefaultClearanceProfileCapacity = 64;
        public const int TelemetryCapacity = 300;
        public const int CsvScratchBytes = 8192;
        public const float TimingUnavailableMicroseconds = -1f;
        public const float DefaultCatastrophicMicrosecondThreshold = 500f;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_310.bin";
        public const string ClearanceCsvRelativePath = "entity_clearance_profiles.csv";
    }

    public static class SpawnValidationFlags
    {
        public const uint Validated = 1u << 0;
        public const uint FailedGeometryIntersection = 1u << 1;
        public const uint ResolvedDearLie = 1u << 2;
        public const uint NonFiniteInput = 1u << 3;
        public const uint SdfUnavailable = 1u << 4;
        public const uint OutOfBoundsClamped = 1u << 5;
        public const uint NearestSampled = 1u << 6;
        public const uint TrilinearSampled = 1u << 7;
        public const uint RollbackExcluded = 1u << 8;
        public const uint TimingUnavailable = 1u << 9;
        public const uint NaNDetected = 1u << 10;
        public const uint BoundsInvalid = 1u << 11;
        public const uint RequestOverflow = 1u << 12;
        public const uint CatastrophicTiming = 1u << 13;
    }

    public static class SpawnSdfGridFlags
    {
        public const uint Valid = 1u << 0;
        public const uint EncodedByteSdf = 1u << 1;
        public const uint SolidPositiveDensity = 1u << 2;
        public const uint MockSdf = 1u << 3;
    }

    public static class SpawnValidationTuningFlags
    {
        public const uint Valid = 1u << 0;
        public const uint EnableDearLie = 1u << 1;
        public const uint TimingExternallyMeasured = 1u << 2;
        public const uint ClearanceProfilesLoaded = 1u << 3;
    }

    [StructLayout(LayoutKind.Explicit, Size = SpawnZoneSdfValidationConstants.RequestSizeBytes)]
    public struct SpawnValidationRequestDTO
    {
        [FieldOffset(0)] public double3 TargetAUP;
        [FieldOffset(24)] public float RequiredClearanceRadius;
        [FieldOffset(28)] public uint ValidationResultFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = SpawnZoneSdfValidationConstants.GridHeaderSizeBytes)]
    public struct SpawnSdfGridHeaderDTO
    {
        [FieldOffset(0)] public double3 OriginAUP;
        [FieldOffset(24)] public int3 GridDimensions;
        [FieldOffset(36)] public float3 CellSizeMeters;
        [FieldOffset(48)] public float SdfRangeMeters;
        [FieldOffset(52)] public uint SdfVersion;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = SpawnZoneSdfValidationConstants.TuningSizeBytes)]
    public struct SpawnValidationTuningDTO
    {
        [FieldOffset(0)] public float GlobalClearanceMultiplier;
        [FieldOffset(4)] public float DearLiePushbackMaxDistance;
        [FieldOffset(8)] public float GlobalQualityWeight;
        [FieldOffset(12)] public float CatastrophicMicrosecondThreshold;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint MaxActiveRequests;
        [FieldOffset(24)] public float FallbackRequiredClearanceRadius;
        [FieldOffset(28)] public uint Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = SpawnZoneSdfValidationConstants.RingStateSizeBytes)]
    public struct SpawnValidationRingStateDTO
    {
        [FieldOffset(0)] public int WriteIndex;
        [FieldOffset(4)] public int ReadIndex;
        [FieldOffset(8)] public int ActiveCount;
        [FieldOffset(12)] public int Capacity;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint Pad0;
        [FieldOffset(28)] public uint Pad1;
        [FieldOffset(32)] public double3 OwnerPhaseAUP;
        [FieldOffset(56)] public ulong Pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = SpawnZoneSdfValidationConstants.TelemetrySizeBytes)]
    public struct SpawnValidationTelemetryEntry
    {
        [FieldOffset(0)] public double3 LastTargetAUP;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public int ValidatedCount;
        [FieldOffset(32)] public int FailedIntersectionCount;
        [FieldOffset(36)] public int DearLieResolvedCount;
        [FieldOffset(40)] public float QueryMicroseconds;
        [FieldOffset(44)] public float GlobalQualityWeight;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint StateHash;
        [FieldOffset(56)] public uint SdfVersion;
        [FieldOffset(60)] public uint SampleModeCounts;
    }

    [StructLayout(LayoutKind.Explicit, Size = SpawnZoneSdfValidationConstants.ProfileSizeBytes)]
    public struct SpawnClearanceProfileDTO
    {
        [FieldOffset(0)] public uint CategoryHash;
        [FieldOffset(4)] public float RequiredClearanceRadius;
        [FieldOffset(8)] public float DearLieMaxPushMeters;
        [FieldOffset(12)] public float ClearanceMultiplier;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint Pad0;
        [FieldOffset(24)] public ulong Pad1;
    }

    public static unsafe class SpawnZoneSdfMath
    {
        private const float Epsilon = 0.0001f;
        private const float TrilinearStartQuality = 0.4f;
        private const float TrilinearFullQuality = 1f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SpawnValidationTuningDTO DefaultTuning()
        {
            SpawnValidationTuningDTO tuning = default;
            tuning.GlobalClearanceMultiplier = 1f;
            tuning.DearLiePushbackMaxDistance = 0.35f;
            tuning.GlobalQualityWeight = 1f;
            tuning.CatastrophicMicrosecondThreshold = SpawnZoneSdfValidationConstants.DefaultCatastrophicMicrosecondThreshold;
            tuning.Flags = SpawnValidationTuningFlags.Valid | SpawnValidationTuningFlags.EnableDearLie;
            tuning.MaxActiveRequests = SpawnZoneSdfValidationConstants.DefaultRequestCapacity;
            tuning.FallbackRequiredClearanceRadius = 0.25f;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SpawnValidationTuningDTO Sanitize(SpawnValidationTuningDTO tuning)
        {
            SpawnValidationTuningDTO fallback = DefaultTuning();
            tuning.GlobalClearanceMultiplier = SanitizePositive(tuning.GlobalClearanceMultiplier, fallback.GlobalClearanceMultiplier);
            tuning.DearLiePushbackMaxDistance = SanitizeNonNegative(tuning.DearLiePushbackMaxDistance, fallback.DearLiePushbackMaxDistance);
            tuning.GlobalQualityWeight = math.saturate(math.select(fallback.GlobalQualityWeight, tuning.GlobalQualityWeight, math.isfinite(tuning.GlobalQualityWeight)));
            tuning.CatastrophicMicrosecondThreshold = SanitizePositive(tuning.CatastrophicMicrosecondThreshold, fallback.CatastrophicMicrosecondThreshold);
            tuning.MaxActiveRequests = tuning.MaxActiveRequests == 0u ? fallback.MaxActiveRequests : tuning.MaxActiveRequests;
            tuning.FallbackRequiredClearanceRadius = SanitizePositive(tuning.FallbackRequiredClearanceRadius, fallback.FallbackRequiredClearanceRadius);
            tuning.Flags |= SpawnValidationTuningFlags.Valid;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryEvaluateRequest(
            ref SpawnValidationRequestDTO request,
            NativeArray<byte>.ReadOnly encodedSdf,
            int sdfLength,
            in SpawnSdfGridHeaderDTO header,
            in SpawnValidationTuningDTO rawTuning,
            out float clearanceMeters,
            out float penetrationMeters)
        {
            clearanceMeters = 0f;
            penetrationMeters = 0f;
            SpawnValidationTuningDTO tuning = Sanitize(rawTuning);
            uint flags = SpawnValidationFlags.Validated | SpawnValidationFlags.RollbackExcluded;

            if (!encodedSdf.IsCreated || sdfLength <= 0 || !TryValidateHeader(in header, sdfLength))
            {
                request.ValidationResultFlags = flags | SpawnValidationFlags.SdfUnavailable;
                return false;
            }

            if (!math.all(math.isfinite(request.TargetAUP)))
            {
                request.ValidationResultFlags = flags | SpawnValidationFlags.NonFiniteInput | SpawnValidationFlags.NaNDetected;
                return false;
            }

            float requiredRadius = ResolveRequiredRadius(request.RequiredClearanceRadius, in tuning);
            request.RequiredClearanceRadius = requiredRadius;
            double3 localDouble = request.TargetAUP - header.OriginAUP;
            if (!math.all(math.isfinite(localDouble)))
            {
                request.ValidationResultFlags = flags | SpawnValidationFlags.NonFiniteInput | SpawnValidationFlags.NaNDetected;
                return false;
            }

            float3 local = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
            if (!math.all(math.isfinite(local)))
            {
                request.ValidationResultFlags = flags | SpawnValidationFlags.NonFiniteInput | SpawnValidationFlags.NaNDetected;
                return false;
            }

            if (!TrySampleClearance(encodedSdf, sdfLength, in header, local, tuning.GlobalQualityWeight, out clearanceMeters, out uint sampleFlags))
            {
                request.ValidationResultFlags = flags | SpawnValidationFlags.SdfUnavailable;
                return false;
            }

            flags |= sampleFlags;
            penetrationMeters = requiredRadius - clearanceMeters;
            if (clearanceMeters < requiredRadius)
                flags |= SpawnValidationFlags.FailedGeometryIntersection;

            request.ValidationResultFlags = flags;
            return (flags & SpawnValidationFlags.FailedGeometryIntersection) == 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryResolveMinorIntersection(
            ref SpawnValidationRequestDTO request,
            NativeArray<byte>.ReadOnly encodedSdf,
            int sdfLength,
            in SpawnSdfGridHeaderDTO header,
            in SpawnValidationTuningDTO rawTuning)
        {
            if ((request.ValidationResultFlags & SpawnValidationFlags.FailedGeometryIntersection) == 0u)
                return true;

            SpawnValidationTuningDTO tuning = Sanitize(rawTuning);
            if ((tuning.Flags & SpawnValidationTuningFlags.EnableDearLie) == 0u)
                return false;

            float clearance;
            float penetration;
            if (!TryEvaluateRequest(ref request, encodedSdf, sdfLength, in header, in tuning, out clearance, out penetration))
            {
                if ((request.ValidationResultFlags & SpawnValidationFlags.FailedGeometryIntersection) == 0u)
                    return false;
            }

            float requiredRadius = ResolveRequiredRadius(request.RequiredClearanceRadius, in tuning);
            float maxPush = math.min(tuning.DearLiePushbackMaxDistance, requiredRadius * 0.15f);
            if (!math.isfinite(penetration) || penetration <= 0f || penetration > maxPush)
                return false;

            double3 localDouble = request.TargetAUP - header.OriginAUP;
            float3 local = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
            float3 gradient = EstimateClearanceGradient(encodedSdf, sdfLength, in header, local, tuning.GlobalQualityWeight);
            float gradientLengthSq = math.lengthsq(gradient);
            if (!math.isfinite(gradientLengthSq) || gradientLengthSq < 0.000001f)
                return false;

            float3 normal = gradient * math.rsqrt(math.max(gradientLengthSq, 0.000001f));
            float push = math.min(maxPush, penetration + 0.01f);
            request.TargetAUP += new double3(normal.x * push, normal.y * push, normal.z * push);

            SpawnValidationRequestDTO probe = request;
            if (!TryEvaluateRequest(ref probe, encodedSdf, sdfLength, in header, in tuning, out clearance, out penetration) &&
                (probe.ValidationResultFlags & SpawnValidationFlags.FailedGeometryIntersection) != 0u)
            {
                return false;
            }

            request.ValidationResultFlags = (probe.ValidationResultFlags & ~SpawnValidationFlags.FailedGeometryIntersection) |
                                            SpawnValidationFlags.ResolvedDearLie |
                                            SpawnValidationFlags.RollbackExcluded;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySampleClearance(
            NativeArray<byte>.ReadOnly encodedSdf,
            int sdfLength,
            in SpawnSdfGridHeaderDTO header,
            float3 local,
            float globalQualityWeight,
            out float clearanceMeters,
            out uint flags)
        {
            clearanceMeters = 0f;
            flags = 0u;
            if (!encodedSdf.IsCreated || !TryValidateHeader(in header, sdfLength))
                return false;

            int3 dimensions = header.GridDimensions;
            float3 cell = SanitizeCellSize(header.CellSizeMeters);
            float3 coord = local / cell;
            float3 maxCoord = new float3(dimensions.x - 1, dimensions.y - 1, dimensions.z - 1);
            bool clamped = math.any(coord < float3.zero) || math.any(coord > maxCoord);
            coord = math.clamp(coord, float3.zero, maxCoord);
            flags |= math.select(0u, SpawnValidationFlags.OutOfBoundsClamped, clamped);

            float q = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            float trilinearBlend = math.smoothstep(TrilinearStartQuality, TrilinearFullQuality, q);
            float nearest = SampleNearestClearance(encodedSdf, sdfLength, in header, coord);
            clearanceMeters = nearest;
            flags |= SpawnValidationFlags.NearestSampled;

            if (trilinearBlend > 0f)
            {
                float trilinear = SampleTrilinearClearance(encodedSdf, sdfLength, in header, coord);
                clearanceMeters = math.lerp(nearest, trilinear, trilinearBlend);
                flags |= SpawnValidationFlags.TrilinearSampled;
            }

            bool finite = math.isfinite(clearanceMeters);
            flags |= math.select(SpawnValidationFlags.NaNDetected, 0u, finite);
            return finite;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SafeVolume(int3 dimensions)
        {
            if (dimensions.x <= 0 || dimensions.y <= 0 || dimensions.z <= 0)
                return 0;

            long xy = (long)dimensions.x * dimensions.y;
            long xyz = xy * dimensions.z;
            return xyz > 0L && xyz <= int.MaxValue ? (int)xyz : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashRequest(in SpawnValidationRequestDTO request, uint hash)
        {
            unchecked
            {
                hash ^= HashDouble(request.TargetAUP.x);
                hash *= 16777619u;
                hash ^= HashDouble(request.TargetAUP.y);
                hash *= 16777619u;
                hash ^= HashDouble(request.TargetAUP.z);
                hash *= 16777619u;
                hash ^= math.asuint(request.RequiredClearanceRadius);
                hash *= 16777619u;
                hash ^= request.ValidationResultFlags;
                hash *= 16777619u;
                return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 EstimateClearanceGradient(
            NativeArray<byte>.ReadOnly encodedSdf,
            int sdfLength,
            in SpawnSdfGridHeaderDTO header,
            float3 local,
            float globalQualityWeight)
        {
            float3 cell = SanitizeCellSize(header.CellSizeMeters);
            float probe = math.max(0.05f, math.cmax(cell) * 0.5f);
            TrySampleClearance(encodedSdf, sdfLength, in header, local + new float3(probe, 0f, 0f), globalQualityWeight, out float xp, out _);
            TrySampleClearance(encodedSdf, sdfLength, in header, local - new float3(probe, 0f, 0f), globalQualityWeight, out float xn, out _);
            TrySampleClearance(encodedSdf, sdfLength, in header, local + new float3(0f, probe, 0f), globalQualityWeight, out float yp, out _);
            TrySampleClearance(encodedSdf, sdfLength, in header, local - new float3(0f, probe, 0f), globalQualityWeight, out float yn, out _);
            TrySampleClearance(encodedSdf, sdfLength, in header, local + new float3(0f, 0f, probe), globalQualityWeight, out float zp, out _);
            TrySampleClearance(encodedSdf, sdfLength, in header, local - new float3(0f, 0f, probe), globalQualityWeight, out float zn, out _);
            float inv = 0.5f / math.max(Epsilon, probe);
            return new float3((xp - xn) * inv, (yp - yn) * inv, (zp - zn) * inv);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SampleNearestClearance(NativeArray<byte>.ReadOnly encodedSdf, int sdfLength, in SpawnSdfGridHeaderDTO header, float3 coord)
        {
            int3 nearest = (int3)math.round(coord);
            nearest = math.clamp(nearest, int3.zero, header.GridDimensions - new int3(1, 1, 1));
            return DecodeClearance(encodedSdf, sdfLength, in header, nearest.x, nearest.y, nearest.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SampleTrilinearClearance(NativeArray<byte>.ReadOnly encodedSdf, int sdfLength, in SpawnSdfGridHeaderDTO header, float3 coord)
        {
            int3 dimensions = header.GridDimensions;
            float3 maxCoord = new float3(dimensions.x - 1, dimensions.y - 1, dimensions.z - 1);
            coord = math.clamp(coord, float3.zero, maxCoord);
            int3 c0 = (int3)math.floor(coord);
            int3 c1 = math.min(c0 + new int3(1, 1, 1), dimensions - new int3(1, 1, 1));
            float3 t = coord - c0;

            float c000 = DecodeClearance(encodedSdf, sdfLength, in header, c0.x, c0.y, c0.z);
            float c100 = DecodeClearance(encodedSdf, sdfLength, in header, c1.x, c0.y, c0.z);
            float c010 = DecodeClearance(encodedSdf, sdfLength, in header, c0.x, c1.y, c0.z);
            float c110 = DecodeClearance(encodedSdf, sdfLength, in header, c1.x, c1.y, c0.z);
            float c001 = DecodeClearance(encodedSdf, sdfLength, in header, c0.x, c0.y, c1.z);
            float c101 = DecodeClearance(encodedSdf, sdfLength, in header, c1.x, c0.y, c1.z);
            float c011 = DecodeClearance(encodedSdf, sdfLength, in header, c0.x, c1.y, c1.z);
            float c111 = DecodeClearance(encodedSdf, sdfLength, in header, c1.x, c1.y, c1.z);

            float x00 = math.lerp(c000, c100, t.x);
            float x10 = math.lerp(c010, c110, t.x);
            float x01 = math.lerp(c001, c101, t.x);
            float x11 = math.lerp(c011, c111, t.x);
            float y0 = math.lerp(x00, x10, t.y);
            float y1 = math.lerp(x01, x11, t.y);
            return math.lerp(y0, y1, t.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DecodeClearance(NativeArray<byte>.ReadOnly encodedSdf, int sdfLength, in SpawnSdfGridHeaderDTO header, int x, int y, int z)
        {
            int3 dimensions = header.GridDimensions;
            int index = x + dimensions.x * (y + dimensions.y * z);
            if ((uint)index >= (uint)sdfLength)
                return 0f;

            float signedDistance = (((float)encodedSdf[index] * (1f / 255f)) * 2f - 1f) * math.max(Epsilon, header.SdfRangeMeters);
            return (header.Flags & SpawnSdfGridFlags.SolidPositiveDensity) != 0u ? -signedDistance : signedDistance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryValidateHeader(in SpawnSdfGridHeaderDTO header, int sdfLength)
        {
            if ((header.Flags & SpawnSdfGridFlags.Valid) == 0u)
                return false;

            int total = SafeVolume(header.GridDimensions);
            return total > 0 &&
                   total <= sdfLength &&
                   math.all(math.isfinite(header.OriginAUP)) &&
                   math.all(math.isfinite(header.CellSizeMeters)) &&
                   math.isfinite(header.SdfRangeMeters) &&
                   header.SdfRangeMeters > Epsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveRequiredRadius(float radius, in SpawnValidationTuningDTO tuning)
        {
            float safe = math.select(tuning.FallbackRequiredClearanceRadius, radius, math.isfinite(radius) && radius > 0f);
            return math.max(0f, safe * math.max(0f, tuning.GlobalClearanceMultiplier));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeCellSize(float3 cell)
        {
            return new float3(
                SanitizePositive(math.abs(cell.x), 1f),
                SanitizePositive(math.abs(cell.y), 1f),
                SanitizePositive(math.abs(cell.z), 1f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value) && value > Epsilon);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeNonNegative(float value, float fallback)
        {
            return math.select(fallback, math.max(0f, value), math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashDouble(double value)
        {
            ulong bits = math.asulong(value);
            uint hash = (uint)bits;
            hash ^= (uint)(bits >> 32);
            return hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct GenerateMockSdfValidationJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<byte> EncodedSdf;
        [NoAlias] public NativeArray<SpawnSdfGridHeaderDTO> Header;
        public double3 OriginAUP;
        public int3 Dimensions;
        public float CellSizeMeters;
        public float SdfRangeMeters;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            int total = SpawnZoneSdfMath.SafeVolume(Dimensions);
            if (total <= 0 || index >= total || index >= EncodedSdf.Length)
                return;

            int x = index % Dimensions.x;
            int yz = index / Dimensions.x;
            int y = yz % Dimensions.y;
            int z = yz / Dimensions.y;
            float safeCell = math.max(0.25f, CellSizeMeters);
            float3 local = (new float3(x, y, z) + 0.5f) * safeCell;
            float q = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            float tile = math.lerp(9f, 18f, q);
            float3 cell = math.floor(local / tile);
            float3 center = (cell + 0.5f) * tile;
            float sphereRadius = tile * math.lerp(0.22f, 0.34f, q);
            float sphereClearance = math.length(local - center) - sphereRadius;
            float3 bounds = (float3)Dimensions * safeCell;
            float boundary = math.cmin(math.min(local, bounds - local));
            float clearance = math.min(sphereClearance, boundary);
            float range = math.max(0.5f, SdfRangeMeters);
            float normalized = math.saturate((math.clamp(clearance, -range, range) / range) * 0.5f + 0.5f);
            EncodedSdf[index] = (byte)math.clamp((int)math.round(normalized * 255f), 0, 255);

            if (index == 0 && Header.IsCreated && Header.Length > 0)
            {
                SpawnSdfGridHeaderDTO header = default;
                header.OriginAUP = OriginAUP;
                header.GridDimensions = Dimensions;
                header.CellSizeMeters = new float3(safeCell, safeCell, safeCell);
                header.SdfRangeMeters = range;
                header.SdfVersion = 0x53484E55u;
                header.Flags = SpawnSdfGridFlags.Valid | SpawnSdfGridFlags.EncodedByteSdf | SpawnSdfGridFlags.MockSdf;
                Header[0] = header;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public unsafe struct EvaluateSdfClearanceJob : IJobParallelFor
    {
        // SAFETY INVARIANT 1: Execute(index) owns exactly one request row. No other index writes that row, and Count is clamped
        // by the scheduler to the request buffer length before this job is scheduled.
        // SAFETY INVARIANT 2: EncodedSdf is read-only and sourced from the VoxelSdfTexture3D Vault lane; Requests is the
        // SHINOBU_310 validation lane. The two buffers have distinct BufferIDs and cannot alias under the Vault route.
        // REJECTED ROUTES: copying Requests into a temporary output array or using per-row managed wrappers would add memory
        // bandwidth and GC risk. In-place DTO mutation is the required cache-local path.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<SpawnValidationRequestDTO> Requests;
        [ReadOnly, NoAlias] public NativeArray<byte>.ReadOnly EncodedSdf;
        public SpawnSdfGridHeaderDTO Header;
        public SpawnValidationTuningDTO Tuning;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)Requests.Length)
                return;

            SpawnValidationRequestDTO* requests = (SpawnValidationRequestDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Requests);
            ref SpawnValidationRequestDTO request = ref UnsafeUtility.AsRef<SpawnValidationRequestDTO>(requests + index);
            SpawnZoneSdfMath.TryEvaluateRequest(ref request, EncodedSdf, EncodedSdf.Length, in Header, in Tuning, out _, out _);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public unsafe struct ResolveMinorIntersectionsJob : IJobParallelFor
    {
        // SAFETY INVARIANT 1: this correction pass is scheduled only after EvaluateSdfClearanceJob, so it never races the first
        // write pass. Each Execute(index) still owns exactly one request row.
        // SAFETY INVARIANT 2: Dear Lie pushback mutates only TargetAUP and flags for the same row; it never appends, swaps,
        // resizes, or touches another request slot. EncodedSdf remains read-only.
        // REJECTED ROUTES: main-thread retry loops and PhysX probes would reintroduce broadphase sync. A separate output array
        // would double request bandwidth for a rare minor-penetration path.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<SpawnValidationRequestDTO> Requests;
        [ReadOnly, NoAlias] public NativeArray<byte>.ReadOnly EncodedSdf;
        public SpawnSdfGridHeaderDTO Header;
        public SpawnValidationTuningDTO Tuning;
        public int Count;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Count || (uint)index >= (uint)Requests.Length)
                return;

            SpawnValidationRequestDTO* requests = (SpawnValidationRequestDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Requests);
            ref SpawnValidationRequestDTO request = ref UnsafeUtility.AsRef<SpawnValidationRequestDTO>(requests + index);
            SpawnZoneSdfMath.TryResolveMinorIntersection(ref request, EncodedSdf, EncodedSdf.Length, in Header, in Tuning);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct SpawnValidationTelemetryReduceJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<SpawnValidationRequestDTO> Requests;
        [NoAlias] public NativeArray<SpawnValidationTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public SpawnValidationTuningDTO Tuning;
        public uint Frame;
        public uint SdfVersion;
        public float QueryMicroseconds;
        public int Count;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0 || !TelemetryCursor.IsCreated || TelemetryCursor.Length <= 0)
                return;

            int safeCount = math.min(math.max(0, Count), Requests.IsCreated ? Requests.Length : 0);
            int failed = 0;
            int resolved = 0;
            uint hash = 2166136261u;
            uint sampleModes = 0u;
            SpawnValidationRequestDTO last = default;
            for (int i = 0; i < safeCount; i++)
            {
                SpawnValidationRequestDTO request = Requests[i];
                last = request;
                uint flags = request.ValidationResultFlags;
                failed += (flags & SpawnValidationFlags.FailedGeometryIntersection) != 0u ? 1 : 0;
                resolved += (flags & SpawnValidationFlags.ResolvedDearLie) != 0u ? 1 : 0;
                sampleModes += (flags & SpawnValidationFlags.TrilinearSampled) != 0u ? 1u : 0u;
                hash = SpawnZoneSdfMath.HashRequest(in request, hash);
            }

            int cursor = (TelemetryCursor[0] + 1) & 0x7FFFFFFF;
            TelemetryCursor[0] = cursor;
            int slot = cursor % TelemetryRing.Length;

            float threshold = math.max(0.001f, Tuning.CatastrophicMicrosecondThreshold);
            float queryUs = math.select(SpawnZoneSdfValidationConstants.TimingUnavailableMicroseconds, QueryMicroseconds, math.isfinite(QueryMicroseconds) && QueryMicroseconds >= 0f);
            uint flagsOut = SpawnValidationFlags.RollbackExcluded;
            flagsOut |= math.select(SpawnValidationFlags.TimingUnavailable, 0u, queryUs >= 0f);
            flagsOut |= math.select(0u, SpawnValidationFlags.CatastrophicTiming, queryUs > threshold);

            SpawnValidationTelemetryEntry entry = default;
            entry.LastTargetAUP = last.TargetAUP;
            entry.Frame = Frame;
            entry.ValidatedCount = safeCount;
            entry.FailedIntersectionCount = failed;
            entry.DearLieResolvedCount = resolved;
            entry.QueryMicroseconds = queryUs;
            entry.GlobalQualityWeight = math.saturate(Tuning.GlobalQualityWeight);
            entry.Flags = flagsOut;
            entry.StateHash = hash;
            entry.SdfVersion = SdfVersion;
            entry.SampleModeCounts = sampleModes;
            TelemetryRing[slot] = entry;
        }
    }

    public static class SpawnZoneSdfValidationScheduler
    {
        public static JobHandle ScheduleEvaluate(
            NativeArray<SpawnValidationRequestDTO> requests,
            int requestCount,
            NativeArray<byte>.ReadOnly encodedSdf,
            in SpawnSdfGridHeaderDTO header,
            in SpawnValidationTuningDTO rawTuning,
            JobHandle inputDependency)
        {
            if (!requests.IsCreated || !encodedSdf.IsCreated)
                return inputDependency;

            int count = math.min(math.max(0, requestCount), requests.Length);
            if (count <= 0)
                return inputDependency;

            SpawnValidationTuningDTO tuning = SpawnZoneSdfMath.Sanitize(rawTuning);
            var evaluate = new EvaluateSdfClearanceJob
            {
                Requests = requests,
                EncodedSdf = encodedSdf,
                Header = header,
                Tuning = tuning,
                Count = count
            };

            JobHandle handle = evaluate.Schedule(count, 32, inputDependency);
            if ((tuning.Flags & SpawnValidationTuningFlags.EnableDearLie) != 0u)
            {
                var resolve = new ResolveMinorIntersectionsJob
                {
                    Requests = requests,
                    EncodedSdf = encodedSdf,
                    Header = header,
                    Tuning = tuning,
                    Count = count
                };
                handle = resolve.Schedule(count, 32, handle);
            }

            return handle;
        }

        public static JobHandle ScheduleTelemetry(
            NativeArray<SpawnValidationRequestDTO> requests,
            int requestCount,
            NativeArray<SpawnValidationTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryCursor,
            in SpawnValidationTuningDTO tuning,
            uint frame,
            uint sdfVersion,
            float queryMicroseconds,
            JobHandle inputDependency)
        {
            var job = new SpawnValidationTelemetryReduceJob
            {
                Requests = requests,
                TelemetryRing = telemetryRing,
                TelemetryCursor = telemetryCursor,
                Tuning = SpawnZoneSdfMath.Sanitize(tuning),
                Frame = frame,
                SdfVersion = sdfVersion,
                QueryMicroseconds = queryMicroseconds,
                Count = requestCount
            };
            return job.Schedule(inputDependency);
        }

        public static bool TryReadVoxelSdfSnapshot(
            IDataVault vault,
            double3 currentOriginAup,
            out NativeArray<byte>.ReadOnly encodedSdf,
            out SpawnSdfGridHeaderDTO header)
        {
            encodedSdf = default;
            header = default;
            if (vault == null ||
                !vault.TryGetGenerationHandle<VoxelSdfPayloadDescriptorDTO>(BufferID.VoxelSdfPayloadDescriptor, out VaultGenerationHandle<VoxelSdfPayloadDescriptorDTO> descriptorHandle) ||
                descriptorHandle.BufferID != unchecked((uint)(int)BufferID.VoxelSdfPayloadDescriptor) ||
                !vault.TryReadOnlyHandle(in descriptorHandle, out NativeArray<VoxelSdfPayloadDescriptorDTO>.ReadOnly descriptors) ||
                !descriptors.IsCreated ||
                descriptors.Length <= 0)
            {
                return false;
            }

            VoxelSdfPayloadDescriptorDTO descriptor = descriptors[0];
            int expectedLength = SpawnZoneSdfMath.SafeVolume(descriptor.GridDimensions);
            if (expectedLength <= 0 ||
                descriptor.ByteCount != expectedLength ||
                descriptor.BufferId != unchecked((uint)(int)BufferID.VoxelSdfTexture3D) ||
                (descriptor.Flags & VoxelSdfPayloadDescriptorDTO.FlagValid) == 0u ||
                !vault.TryGetGenerationHandle<byte>(BufferID.VoxelSdfTexture3D, out VaultGenerationHandle<byte> sdfHandle) ||
                sdfHandle.BufferID != unchecked((uint)(int)BufferID.VoxelSdfTexture3D) ||
                sdfHandle.Generation != descriptor.BufferGeneration ||
                !vault.TryReadOnlyHandle(in sdfHandle, out encodedSdf) ||
                !encodedSdf.IsCreated ||
                encodedSdf.Length < expectedLength)
            {
                encodedSdf = default;
                return false;
            }

            float3 cell = new float3(
                math.max(0.0001f, math.abs(descriptor.VoxelCellSize.x)),
                math.max(0.0001f, math.abs(descriptor.VoxelCellSize.y)),
                math.max(0.0001f, math.abs(descriptor.VoxelCellSize.z)));
            header.OriginAUP = currentOriginAup + new double3(descriptor.VolumeOrigin.x, descriptor.VolumeOrigin.y, descriptor.VolumeOrigin.z);
            header.GridDimensions = descriptor.GridDimensions;
            header.CellSizeMeters = cell;
            header.SdfRangeMeters = math.max(0.0001f, descriptor.SdfRangeMeters);
            header.SdfVersion = descriptor.SdfVersion;
            header.Flags = SpawnSdfGridFlags.Valid | SpawnSdfGridFlags.EncodedByteSdf | SpawnSdfGridFlags.SolidPositiveDensity;
            return math.all(math.isfinite(header.OriginAUP)) && math.all(math.isfinite(header.CellSizeMeters));
        }
    }

    public static class SpawnClearanceProfileHash
    {
        public static uint HashCategory(ReadOnlySpan<char> chars)
        {
            int start = 0;
            int end = chars.Length - 1;
            while (start <= end && IsSpace(chars[start]))
                start++;
            while (end >= start && IsSpace(chars[end]))
                end--;
            if (start > end)
                return 0u;

            uint hash = 2166136261u;
            for (int i = start; i <= end; i++)
            {
                char c = chars[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                hash ^= c <= 127 ? (byte)c : (byte)'?';
                hash *= 16777619u;
            }

            return hash;
        }

        private static bool IsSpace(char value)
        {
            return value == ' ' || value == '\t' || value == '\r' || value == '\n';
        }
    }

#if UNITY_EDITOR
    public static class SpawnClearanceProfileCsvParser
    {
        public static bool TryParse(ReadOnlySpan<byte> bytes, NativeArray<SpawnClearanceProfileDTO> profiles, out int count, out uint fileHash)
        {
            count = 0;
            fileHash = 2166136261u;
            if (!profiles.IsCreated || profiles.Length <= 0)
                return false;

            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;

            int index = 0;
            int row = 0;
            while (index < bytes.Length && count < profiles.Length)
            {
                SkipLineBreaks(bytes, ref index);
                if (index >= bytes.Length)
                    break;

                int nameStart = index;
                while (index < bytes.Length && bytes[index] != (byte)',' && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                {
                    fileHash = HashByte(fileHash, bytes[index]);
                    index++;
                }

                int nameLength = index - nameStart;
                if (index >= bytes.Length || bytes[index] != (byte)',')
                {
                    SkipToNextLine(bytes, ref index);
                    row++;
                    continue;
                }

                index++;
                if (!TryReadFloat(bytes, ref index, out float radius))
                {
                    SkipToNextLine(bytes, ref index);
                    row++;
                    continue;
                }

                float push = 0.35f;
                float multiplier = 1f;
                if (index < bytes.Length && bytes[index] == (byte)',')
                {
                    index++;
                    TryReadFloat(bytes, ref index, out push);
                }

                if (index < bytes.Length && bytes[index] == (byte)',')
                {
                    index++;
                    TryReadFloat(bytes, ref index, out multiplier);
                }

                if (nameLength > 0 && row > 0)
                {
                    SpawnClearanceProfileDTO profile = default;
                    profile.CategoryHash = HashCategory(bytes.Slice(nameStart, nameLength));
                    profile.RequiredClearanceRadius = math.max(0f, radius);
                    profile.DearLieMaxPushMeters = math.max(0f, push);
                    profile.ClearanceMultiplier = math.max(0f, multiplier);
                    profile.Flags = 1u;
                    profiles[count++] = profile;
                }

                SkipToNextLine(bytes, ref index);
                row++;
            }

            return count > 0;
        }

        public static uint HashCategory(ReadOnlySpan<byte> bytes)
        {
            int start = 0;
            int end = bytes.Length - 1;
            while (start <= end && IsSpace(bytes[start]))
                start++;
            while (end >= start && IsSpace(bytes[end]))
                end--;
            if (start > end)
                return 0u;

            uint hash = 2166136261u;
            for (int i = start; i <= end; i++)
                hash = HashByte(hash, ToLowerAscii(bytes[i]));
            return hash;
        }

        public static uint HashCategory(ReadOnlySpan<char> chars)
        {
            int start = 0;
            int end = chars.Length - 1;
            while (start <= end && IsSpace(chars[start]))
                start++;
            while (end >= start && IsSpace(chars[end]))
                end--;
            if (start > end)
                return 0u;

            uint hash = 2166136261u;
            for (int i = start; i <= end; i++)
            {
                char c = chars[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                hash ^= c <= 127 ? (byte)c : (byte)'?';
                hash *= 16777619u;
            }
            return hash;
        }

        private static bool TryReadFloat(ReadOnlySpan<byte> bytes, ref int index, out float value)
        {
            value = 0f;
            SkipSpaces(bytes, ref index);
            int sign = 1;
            if (index < bytes.Length && bytes[index] == (byte)'-')
            {
                sign = -1;
                index++;
            }

            double whole = 0d;
            double frac = 0d;
            double place = 0.1d;
            bool any = false;
            while (index < bytes.Length)
            {
                byte c = bytes[index];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                whole = whole * 10d + c - (byte)'0';
                index++;
                any = true;
            }

            if (index < bytes.Length && bytes[index] == (byte)'.')
            {
                index++;
                while (index < bytes.Length)
                {
                    byte c = bytes[index];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;
                    frac += (c - (byte)'0') * place;
                    place *= 0.1d;
                    index++;
                    any = true;
                }
            }

            value = (float)((whole + frac) * sign);
            return any && math.isfinite(value);
        }

        private static uint HashBytes(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
                hash = HashByte(hash, bytes[i]);
            return hash;
        }

        private static uint HashByte(uint hash, byte value)
        {
            unchecked
            {
                hash ^= value;
                hash *= 16777619u;
                return hash;
            }
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private static bool IsSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }

        private static bool IsSpace(char value)
        {
            return value == ' ' || value == '\t' || value == '\r' || value == '\n';
        }

        private static void SkipSpaces(ReadOnlySpan<byte> bytes, ref int index)
        {
            while (index < bytes.Length && (bytes[index] == (byte)' ' || bytes[index] == (byte)'\t'))
                index++;
        }

        private static void SkipLineBreaks(ReadOnlySpan<byte> bytes, ref int index)
        {
            while (index < bytes.Length && (bytes[index] == (byte)'\r' || bytes[index] == (byte)'\n'))
                index++;
        }

        private static void SkipToNextLine(ReadOnlySpan<byte> bytes, ref int index)
        {
            while (index < bytes.Length && bytes[index] != (byte)'\n')
                index++;
            SkipLineBreaks(bytes, ref index);
        }
    }
#endif

    public static class SpawnZoneSdfForensics
    {
        private const ulong DumpMagic = 0x3031335F4E485348UL;
        private const int DumpVersion = 1;
        private const string DumpPayloadLabel = "spawnZoneSdfTelemetryDumpPayload";

        public static unsafe void WriteTelemetryDump(string projectRoot, NativeArray<SpawnValidationTelemetryEntry> telemetry, int cursor)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0 || string.IsNullOrEmpty(projectRoot))
                return;

            string path = Path.Combine(projectRoot, SpawnZoneSdfValidationConstants.DumpRelativePath);
            int entrySize = UnsafeUtility.SizeOf<SpawnValidationTelemetryEntry>();
            int totalBytes = 24 + telemetry.Length * entrySize;
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                totalBytes,
                nameof(SpawnZoneSdfForensics),
                DumpPayloadLabel,
                NativeArrayOptions.ClearMemory);

            try
            {
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                Span<byte> header = new Span<byte>(destination, 24);
                BinaryPrimitives.WriteUInt64LittleEndian(header.Slice(0, 8), DumpMagic);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(8, 4), DumpVersion);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(12, 4), telemetry.Length);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(16, 4), cursor);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(20, 4), entrySize);

                int start = cursor - telemetry.Length;
                if (start < 0)
                    start = 0;

                int offset = 24;
                for (int i = 0; i < telemetry.Length; i++)
                {
                    int slot = (start + i) % telemetry.Length;
                    SpawnValidationTelemetryEntry entry = telemetry[slot];
                    UnsafeUtility.MemCpy(destination + offset, &entry, entrySize);
                    offset += entrySize;
                }

                NativeFaultDumpWriter.TryWriteAll(path, payload, totalBytes);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(SpawnZoneSdfForensics),
                    DumpPayloadLabel);
            }
        }
    }

    public sealed partial class ResourceDistributionDirector
    {
        private const SystemID SpawnSdfValidationOwner = SystemID.WorldResourceSpawnerRuntime;

        private IDataVault _spawnSdfDataVault;
        private VaultGenerationHandle<SpawnValidationRequestDTO> _spawnValidationRequestHandle;
        private VaultGenerationHandle<SpawnValidationRingStateDTO> _spawnValidationRingStateHandle;
        private VaultGenerationHandle<SpawnValidationTelemetryEntry> _spawnValidationTelemetryHandle;
        private VaultGenerationHandle<int> _spawnValidationTelemetryCursorHandle;
        private VaultGenerationHandle<SpawnValidationTuningDTO> _spawnValidationTuningHandle;
        private VaultGenerationHandle<SpawnClearanceProfileDTO> _spawnClearanceProfilesHandle;
        private VaultGenerationHandle<byte> _spawnClearanceCsvScratchHandle;
        private bool _spawnClearanceProfilesLoadedCold;

        private void CacheSpawnSdfValidationServicesCold()
        {
            if (_spawnSdfDataVault == null)
                _spawnSdfDataVault = GlobalRegistry.DataVault;

            EnsureSpawnSdfValidationVaultBuffers();
        }

        private void ClearSpawnSdfValidationServicesCold()
        {
            _spawnSdfDataVault = null;
            _spawnClearanceProfilesLoadedCold = false;
        }

        private void EnsureSpawnSdfValidationVaultBuffers()
        {
            IDataVault vault = _spawnSdfDataVault;
            if (vault == null)
                return;

            _spawnValidationRequestHandle = vault.EnsureGenerationHandle<SpawnValidationRequestDTO>(
                BufferID.ShinobuSpawnSdfValidationRequests,
                math.max(SpawnZoneSdfValidationConstants.DefaultRequestCapacity, maxSpawnsPerSlowTick),
                SpawnSdfValidationOwner,
                NativeArrayOptions.UninitializedMemory);
            _spawnValidationRingStateHandle = vault.EnsureGenerationHandle<SpawnValidationRingStateDTO>(
                BufferID.ShinobuSpawnSdfValidationRingState,
                1,
                SpawnSdfValidationOwner,
                NativeArrayOptions.ClearMemory);
            _spawnValidationTelemetryHandle = vault.EnsureGenerationHandle<SpawnValidationTelemetryEntry>(
                BufferID.ShinobuSpawnSdfValidationTelemetryRing,
                SpawnZoneSdfValidationConstants.TelemetryCapacity,
                SpawnSdfValidationOwner,
                NativeArrayOptions.ClearMemory);
            _spawnValidationTelemetryCursorHandle = vault.EnsureGenerationHandle<int>(
                BufferID.ShinobuSpawnSdfValidationTelemetryCursor,
                1,
                SpawnSdfValidationOwner,
                NativeArrayOptions.ClearMemory);
            _spawnValidationTuningHandle = vault.EnsureGenerationHandle<SpawnValidationTuningDTO>(
                BufferID.ShinobuSpawnSdfValidationTuning,
                1,
                SpawnSdfValidationOwner,
                NativeArrayOptions.ClearMemory);
            _spawnClearanceProfilesHandle = vault.EnsureGenerationHandle<SpawnClearanceProfileDTO>(
                BufferID.ShinobuSpawnClearanceProfiles,
                SpawnZoneSdfValidationConstants.DefaultClearanceProfileCapacity,
                SpawnSdfValidationOwner,
                NativeArrayOptions.ClearMemory);
            _spawnClearanceCsvScratchHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.ShinobuSpawnClearanceCsvScratch,
                SpawnZoneSdfValidationConstants.CsvScratchBytes,
                SpawnSdfValidationOwner,
                NativeArrayOptions.UninitializedMemory);

            if (vault.TryResolveHandle(in _spawnValidationTuningHandle, out NativeArray<SpawnValidationTuningDTO> tuning) &&
                tuning.IsCreated &&
                tuning.Length > 0 &&
                (tuning[0].Flags & SpawnValidationTuningFlags.Valid) == 0u)
            {
                tuning[0] = SpawnZoneSdfMath.DefaultTuning();
            }

#if UNITY_EDITOR
            TryLoadSpawnClearanceProfilesCold(vault);
#endif
        }

        private unsafe bool TryValidateSpawnRuntimePositionViaSdf(
            Vector3 runtimePosition,
            float requiredClearanceRadius,
            out Vector3 resolvedRuntimePosition)
        {
            resolvedRuntimePosition = runtimePosition;
            IDataVault vault = _spawnSdfDataVault;
            if (vault == null)
                return !Application.isPlaying;

            SpawnValidationTuningDTO tuning = ResolveSpawnValidationTuning();
            SpawnValidationRequestDTO request = default;
            request.TargetAUP = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimePosition);
            request.RequiredClearanceRadius = requiredClearanceRadius;
            request.ValidationResultFlags = 0u;

            double3 originAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!SpawnZoneSdfValidationScheduler.TryReadVoxelSdfSnapshot(vault, originAup, out NativeArray<byte>.ReadOnly encodedSdf, out SpawnSdfGridHeaderDTO header))
            {
                request.ValidationResultFlags = SpawnValidationFlags.Validated |
                                                SpawnValidationFlags.SdfUnavailable |
                                                SpawnValidationFlags.RollbackExcluded;
                StoreSpawnValidationRequestSnapshot(in request);
                RecordSingleSpawnValidationTelemetry(in request, in tuning, 0u);
                return false;
            }

            bool valid = SpawnZoneSdfMath.TryEvaluateRequest(ref request, encodedSdf, encodedSdf.Length, in header, in tuning, out _, out _);
            if (!valid && (tuning.Flags & SpawnValidationTuningFlags.EnableDearLie) != 0u)
                valid = SpawnZoneSdfMath.TryResolveMinorIntersection(ref request, encodedSdf, encodedSdf.Length, in header, in tuning);

            if (valid)
                resolvedRuntimePosition = HectonFloatingOrigin.ToRuntimePosition(request.TargetAUP);

            StoreSpawnValidationRequestSnapshot(in request);
            RecordSingleSpawnValidationTelemetry(in request, in tuning, header.SdfVersion);
            return valid;
        }

        private float ResolveSpawnSdfRequiredClearanceRadius(Hecton8.Scavenging.ResourceNodeTemplate template)
        {
            if (template == null)
                return SpawnZoneSdfMath.DefaultTuning().FallbackRequiredClearanceRadius;

            float fallback = math.max(0.05f, template.SpawnOffsetMeters);
            IDataVault vault = _spawnSdfDataVault;
            if (vault == null ||
                !vault.TryReadOnlyHandle(in _spawnClearanceProfilesHandle, out NativeArray<SpawnClearanceProfileDTO>.ReadOnly profiles) ||
                !profiles.IsCreated ||
                profiles.Length <= 0)
            {
                return fallback;
            }

            uint categoryHash = SpawnClearanceProfileHash.HashCategory(template.StableId.AsSpan());
            if (categoryHash == 0u)
                return fallback;

            for (int i = 0; i < profiles.Length; i++)
            {
                SpawnClearanceProfileDTO profile = profiles[i];
                if ((profile.Flags & 1u) == 0u || profile.CategoryHash != categoryHash)
                    continue;

                float radius = profile.RequiredClearanceRadius * math.max(0f, profile.ClearanceMultiplier);
                return math.max(0.05f, math.select(fallback, radius, math.isfinite(radius) && radius > 0f));
            }

            return fallback;
        }

        private SpawnValidationTuningDTO ResolveSpawnValidationTuning()
        {
            IDataVault vault = _spawnSdfDataVault;
            if (vault != null &&
                vault.TryReadOnlyHandle(in _spawnValidationTuningHandle, out NativeArray<SpawnValidationTuningDTO>.ReadOnly tuning) &&
                tuning.IsCreated &&
                tuning.Length > 0)
            {
                return SpawnZoneSdfMath.Sanitize(tuning[0]);
            }

            return SpawnZoneSdfMath.DefaultTuning();
        }

        private void StoreSpawnValidationRequestSnapshot(in SpawnValidationRequestDTO request)
        {
            IDataVault vault = _spawnSdfDataVault;
            if (vault == null ||
                !vault.TryResolveHandle(in _spawnValidationRequestHandle, out NativeArray<SpawnValidationRequestDTO> requests) ||
                !vault.TryResolveHandle(in _spawnValidationRingStateHandle, out NativeArray<SpawnValidationRingStateDTO> ringState) ||
                !requests.IsCreated ||
                requests.Length <= 0 ||
                !ringState.IsCreated ||
                ringState.Length <= 0)
            {
                return;
            }

            SpawnValidationRingStateDTO state = ringState[0];
            SpawnValidationTuningDTO tuning = ResolveSpawnValidationTuning();
            int maxActive = tuning.MaxActiveRequests > int.MaxValue ? int.MaxValue : (int)tuning.MaxActiveRequests;
            int capacity = math.min(requests.Length, math.max(1, maxActive));
            int slot = math.abs(state.WriteIndex) % capacity;
            requests[slot] = request;
            state.WriteIndex = state.WriteIndex + 1;
            state.ActiveCount = math.min(capacity, state.ActiveCount + 1);
            state.Capacity = capacity;
            state.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            state.Flags = SpawnValidationFlags.RollbackExcluded;
            state.OwnerPhaseAUP = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            ringState[0] = state;
        }

        private void RecordSingleSpawnValidationTelemetry(
            in SpawnValidationRequestDTO request,
            in SpawnValidationTuningDTO tuning,
            uint sdfVersion)
        {
            IDataVault vault = _spawnSdfDataVault;
            if (vault == null ||
                !vault.TryResolveHandle(in _spawnValidationTelemetryHandle, out NativeArray<SpawnValidationTelemetryEntry> telemetry) ||
                !vault.TryResolveHandle(in _spawnValidationTelemetryCursorHandle, out NativeArray<int> cursor) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0 ||
                !cursor.IsCreated ||
                cursor.Length <= 0)
            {
                return;
            }

            int nextCursor = (cursor[0] + 1) & 0x7FFFFFFF;
            cursor[0] = nextCursor;
            int slot = nextCursor % telemetry.Length;
            SpawnValidationTelemetryEntry entry = default;
            entry.LastTargetAUP = request.TargetAUP;
            entry.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            entry.ValidatedCount = 1;
            entry.FailedIntersectionCount = (request.ValidationResultFlags & SpawnValidationFlags.FailedGeometryIntersection) != 0u ? 1 : 0;
            entry.DearLieResolvedCount = (request.ValidationResultFlags & SpawnValidationFlags.ResolvedDearLie) != 0u ? 1 : 0;
            entry.QueryMicroseconds = SpawnZoneSdfValidationConstants.TimingUnavailableMicroseconds;
            entry.GlobalQualityWeight = tuning.GlobalQualityWeight;
            entry.Flags = request.ValidationResultFlags | SpawnValidationFlags.TimingUnavailable | SpawnValidationFlags.RollbackExcluded;
            entry.StateHash = SpawnZoneSdfMath.HashRequest(in request, 2166136261u);
            entry.SdfVersion = sdfVersion;
            entry.SampleModeCounts = (request.ValidationResultFlags & SpawnValidationFlags.TrilinearSampled) != 0u ? 1u : 0u;
            telemetry[slot] = entry;

            if ((entry.Flags & (SpawnValidationFlags.NaNDetected | SpawnValidationFlags.BoundsInvalid)) != 0u)
                SpawnZoneSdfForensics.WriteTelemetryDump(ResolveProjectRootForSpawnSdfDump(), telemetry, cursor[0]);
        }

#if UNITY_EDITOR
        private unsafe void TryLoadSpawnClearanceProfilesCold(IDataVault vault)
        {
            if (_spawnClearanceProfilesLoadedCold || vault == null)
                return;

            _spawnClearanceProfilesLoadedCold = true;
            if (!vault.TryResolveHandle(in _spawnClearanceProfilesHandle, out NativeArray<SpawnClearanceProfileDTO> profiles) ||
                !vault.TryResolveHandle(in _spawnClearanceCsvScratchHandle, out NativeArray<byte> scratch) ||
                !profiles.IsCreated ||
                profiles.Length <= 0 ||
                !scratch.IsCreated ||
                scratch.Length <= 0)
            {
                return;
            }

            string path = ResolveSpawnClearanceCsvPath();
            int byteCount = TryReadFileIntoScratch(path, scratch);
            if (byteCount <= 0)
                return;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
            if (SpawnClearanceProfileCsvParser.TryParse(new ReadOnlySpan<byte>(ptr, byteCount), profiles, out int count, out _) &&
                count > 0 &&
                vault.TryResolveHandle(in _spawnValidationTuningHandle, out NativeArray<SpawnValidationTuningDTO> tuning) &&
                tuning.IsCreated &&
                tuning.Length > 0)
            {
                SpawnValidationTuningDTO row = SpawnZoneSdfMath.Sanitize(tuning[0]);
                row.Flags |= SpawnValidationTuningFlags.ClearanceProfilesLoaded;
                tuning[0] = row;
            }
        }
#endif

        private static unsafe int TryReadFileIntoScratch(string path, NativeArray<byte> scratch)
        {
            if (string.IsNullOrEmpty(path) || !scratch.IsCreated || scratch.Length <= 0 || !File.Exists(path))
                return 0;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
                {
                    int byteCount = (int)math.min(stream.Length, scratch.Length);
                    if (byteCount <= 0)
                        return 0;

                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                    Span<byte> destination = new Span<byte>(ptr, byteCount);
                    int total = 0;
                    while (total < byteCount)
                    {
                        int read = stream.Read(destination.Slice(total));
                        if (read <= 0)
                            break;

                        total += read;
                    }

                    return total;
                }
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }

        private static string ResolveSpawnClearanceCsvPath()
        {
            string streamingRoot = Application.streamingAssetsPath;
            string streamingDirect = Path.Combine(streamingRoot, SpawnZoneSdfValidationConstants.ClearanceCsvRelativePath);
            if (File.Exists(streamingDirect))
                return streamingDirect;

            string monolithPath = Path.Combine(streamingRoot, "Hecton8", "DataMonolith", SpawnZoneSdfValidationConstants.ClearanceCsvRelativePath);
            if (File.Exists(monolithPath))
                return monolithPath;

            string projectRoot = ResolveProjectRootForSpawnSdfDump();
            if (!string.IsNullOrEmpty(projectRoot))
            {
                string projectDataPath = Path.Combine(projectRoot, "Assets", "_Project", "Data", SpawnZoneSdfValidationConstants.ClearanceCsvRelativePath);
                if (File.Exists(projectDataPath))
                    return projectDataPath;

                string rootPath = Path.Combine(projectRoot, SpawnZoneSdfValidationConstants.ClearanceCsvRelativePath);
                if (File.Exists(rootPath))
                    return rootPath;
            }

            return streamingDirect;
        }

        private static string ResolveProjectRootForSpawnSdfDump()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName : Application.dataPath;
        }
    }
}
