using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Physics.Vehicles;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Vehicles.Automation
{
    public static class SubmarineAutopilotConstants
    {
        public const int MaxVehicles = SubmarineDynamicsConstants.MaxVehicles;
        public const int MaxFeelersPerVehicle = 32;
        public const int MinFeelersPerVehicle = 5;
        public const int BlackBoxFrames = 300;
        public const int MockSdfWidth = 48;
        public const int MockSdfHeight = 24;
        public const int MockSdfDepth = 48;
        public const int MockSdfVoxelCount = MockSdfWidth * MockSdfHeight * MockSdfDepth;
        public const int FlowWidth = 16;
        public const int FlowHeight = 8;
        public const int FlowDepth = 16;
        public const int FlowSampleCount = FlowWidth * FlowHeight * FlowDepth;
        public const int WaypointCapacity = 256;
        public const int HandlingProfileCapacity = 32;
        public const int CsvScratchBytes = 4096;

        public const uint HandlingProfileDefaultHash = 0x933B5BDEu;
        public const uint HandlingProfileScoutHash = 0xC322AD05u;
        public const uint HandlingProfileFreighterHash = 0x8376F77Bu;

        public const uint NavFlagActive = 1u << 0;
        public const uint NavFlagInitialized = 1u << 1;
        public const uint NavFlagSdfFallback = 1u << 2;
        public const uint NavFlagFlowCompensated = 1u << 3;
        public const uint NavFlagWaypointAdvanced = 1u << 4;
        public const uint NavFlagAtTarget = 1u << 5;
        public const uint NavFlagFatalNaN = 1u << 30;
        public const uint NavFlagSlowBurst = 1u << 29;

        public const uint FeelerFlagActive = 1u << 0;
        public const uint FeelerFlagHit = 1u << 1;
        public const uint FeelerFlagGradientValid = 1u << 2;
        public const uint FeelerFlagSdfMissing = 1u << 3;
        public const uint WaypointFlagActive = 1u << 0;
        public const uint RouteFlagActive = 1u << 0;

        public const uint SourceHashMockSdf = 0x53444631u; // 1FDS
        public const uint SourceHashAutopilot = 0x41503135u; // 51PA
    }

    public static class SubmarineAutopilotVaultRoute
    {
        public const BufferID AutopilotStates = (BufferID)71592;
        public const BufferID AutopilotAvoidance = (BufferID)71593;
        public const BufferID AutopilotFeelerResults = (BufferID)71594;
        public const BufferID AutopilotWaypoints = (BufferID)71595;
        public const BufferID AutopilotRouteRanges = (BufferID)71596;
        public const BufferID AutopilotTuning = (BufferID)71597;
        public const BufferID AutopilotTelemetryRing = (BufferID)71598;
        public const BufferID AutopilotTelemetryCursor = (BufferID)71599;
        public const BufferID AutopilotMockSdf = (BufferID)71600;
        public const BufferID AutopilotFlowSamples = (BufferID)71601;
        public const BufferID AutopilotCsvScratch = (BufferID)71602;
        public const BufferID AutopilotHandlingProfiles = (BufferID)71603;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AutopilotStateDTO
    {
        [FieldOffset(0)] public double3 TargetAUP;
        [FieldOffset(24)] public float3 DesiredVelocity;
        [FieldOffset(36)] public float TargetSpeed;
        [FieldOffset(40)] public uint SubmarineHashID;
        [FieldOffset(44)] public uint NavFlags;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AutopilotAvoidanceDTO
    {
        [FieldOffset(0)] public float3 Repulsion;
        [FieldOffset(12)] public float3 Forward;
        [FieldOffset(24)] public float3 FlowVelocity;
        [FieldOffset(36)] public float AverageSdfPressure01;
        [FieldOffset(40)] public float NearestHitDistance;
        [FieldOffset(44)] public uint ActiveFeelerCount;
        [FieldOffset(48)] public uint HitFeelerCount;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AutopilotFeelerResultDTO
    {
        [FieldOffset(0)] public float3 StartRuntime;
        [FieldOffset(12)] public float3 EndRuntime;
        [FieldOffset(24)] public float3 HitRuntime;
        [FieldOffset(36)] public float3 Repulsion;
        [FieldOffset(48)] public float HitDistance;
        [FieldOffset(52)] public float SdfDensity;
        [FieldOffset(56)] public uint FeelerIndex;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AutopilotWaypointDTO
    {
        [FieldOffset(0)] public double3 TargetAUP;
        [FieldOffset(24)] public float AcceptanceRadius;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AutopilotRouteRangeDTO
    {
        [FieldOffset(0)] public int StartIndex;
        [FieldOffset(4)] public int Count;
        [FieldOffset(8)] public int CurrentOffset;
        [FieldOffset(12)] public float AcceptanceRadius;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint RouteHash;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct AutopilotTuningDTO
    {
        [FieldOffset(0)] public float FeelerLength;
        [FieldOffset(4)] public float SdfThresholdMeters;
        [FieldOffset(8)] public float RepulsionWeight;
        [FieldOffset(12)] public float MaxTurnRateRadians;
        [FieldOffset(16)] public float WaypointAcceptanceRadius;
        [FieldOffset(20)] public float FlowCompensationWeight;
        [FieldOffset(24)] public float TargetSpeedFallback;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public float3 SdfOrigin;
        [FieldOffset(44)] public float3 SdfCellSize;
        [FieldOffset(56)] public int3 SdfDimensions;
        [FieldOffset(68)] public float SdfRangeMeters;
        [FieldOffset(72)] public uint Flags;
        [FieldOffset(76)] public int ActiveVehicleCount;
        [FieldOffset(80)] public float3 FlowOrigin;
        [FieldOffset(92)] public float3 FlowCellSize;
        [FieldOffset(104)] public int3 FlowDimensions;
        [FieldOffset(116)] public uint SourceHash;
        [FieldOffset(120)] public float ResolvedQualityWeight;
        [FieldOffset(124)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AutopilotTelemetryEntry
    {
        [FieldOffset(0)] public double3 FirstAUP;
        [FieldOffset(24)] public float3 AverageRepulsion;
        [FieldOffset(36)] public float AverageRepulsionMagnitude;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint ActiveAutopilots;
        [FieldOffset(48)] public uint FeelerCount;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float EstimatedBurstMicroseconds;
        [FieldOffset(60)] public uint StateHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AutopilotHandlingProfileDTO
    {
        [FieldOffset(0)] public uint NameHash;
        [FieldOffset(4)] public float MaxTurnRateRadians;
        [FieldOffset(8)] public float AccelerationLimit;
        [FieldOffset(12)] public float SpeedScale;
        [FieldOffset(16)] public float RepulsionWeight;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

#if UNITY_EDITOR
    public static class AutopilotStateDTOLayout
    {
        public static bool ValidateAll()
        {
            return Validate() &&
                   ValidateAvoidance() &&
                   ValidateFeelerResult() &&
                   ValidateWaypoint() &&
                   ValidateRouteRange() &&
                   ValidateTuning() &&
                   ValidateTelemetry() &&
                   ValidateHandlingProfile();
        }

        public static bool Validate()
        {
            return UnsafeUtility.SizeOf<AutopilotStateDTO>() == 64 &&
                   OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO.TargetAUP)) == 0 &&
                   OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO.DesiredVelocity)) == 24 &&
                   OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO.TargetSpeed)) == 36 &&
                   OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO.SubmarineHashID)) == 40 &&
                   OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO.NavFlags)) == 44 &&
                   OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO._pad0)) == 48 &&
                   OffsetOf<AutopilotStateDTO>(nameof(AutopilotStateDTO._pad1)) == 56;
        }

        public static bool ValidateAvoidance()
        {
            return UnsafeUtility.SizeOf<AutopilotAvoidanceDTO>() == 64 &&
                   OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.Repulsion)) == 0 &&
                   OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.Forward)) == 12 &&
                   OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.FlowVelocity)) == 24 &&
                   OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.AverageSdfPressure01)) == 36 &&
                   OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.NearestHitDistance)) == 40 &&
                   OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.ActiveFeelerCount)) == 44 &&
                   OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.HitFeelerCount)) == 48 &&
                   OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO.Flags)) == 52 &&
                   OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO._pad0)) == 56 &&
                   OffsetOf<AutopilotAvoidanceDTO>(nameof(AutopilotAvoidanceDTO._pad1)) == 60;
        }

        public static bool ValidateFeelerResult()
        {
            return UnsafeUtility.SizeOf<AutopilotFeelerResultDTO>() == 64 &&
                   OffsetOf<AutopilotFeelerResultDTO>(nameof(AutopilotFeelerResultDTO.StartRuntime)) == 0 &&
                   OffsetOf<AutopilotFeelerResultDTO>(nameof(AutopilotFeelerResultDTO.EndRuntime)) == 12 &&
                   OffsetOf<AutopilotFeelerResultDTO>(nameof(AutopilotFeelerResultDTO.HitRuntime)) == 24 &&
                   OffsetOf<AutopilotFeelerResultDTO>(nameof(AutopilotFeelerResultDTO.Repulsion)) == 36 &&
                   OffsetOf<AutopilotFeelerResultDTO>(nameof(AutopilotFeelerResultDTO.HitDistance)) == 48 &&
                   OffsetOf<AutopilotFeelerResultDTO>(nameof(AutopilotFeelerResultDTO.SdfDensity)) == 52 &&
                   OffsetOf<AutopilotFeelerResultDTO>(nameof(AutopilotFeelerResultDTO.FeelerIndex)) == 56 &&
                   OffsetOf<AutopilotFeelerResultDTO>(nameof(AutopilotFeelerResultDTO.Flags)) == 60;
        }

        public static bool ValidateWaypoint()
        {
            return UnsafeUtility.SizeOf<AutopilotWaypointDTO>() == 32 &&
                   OffsetOf<AutopilotWaypointDTO>(nameof(AutopilotWaypointDTO.TargetAUP)) == 0 &&
                   OffsetOf<AutopilotWaypointDTO>(nameof(AutopilotWaypointDTO.AcceptanceRadius)) == 24 &&
                   OffsetOf<AutopilotWaypointDTO>(nameof(AutopilotWaypointDTO.Flags)) == 28;
        }

        public static bool ValidateRouteRange()
        {
            return UnsafeUtility.SizeOf<AutopilotRouteRangeDTO>() == 32 &&
                   OffsetOf<AutopilotRouteRangeDTO>(nameof(AutopilotRouteRangeDTO.StartIndex)) == 0 &&
                   OffsetOf<AutopilotRouteRangeDTO>(nameof(AutopilotRouteRangeDTO.Count)) == 4 &&
                   OffsetOf<AutopilotRouteRangeDTO>(nameof(AutopilotRouteRangeDTO.CurrentOffset)) == 8 &&
                   OffsetOf<AutopilotRouteRangeDTO>(nameof(AutopilotRouteRangeDTO.AcceptanceRadius)) == 12 &&
                   OffsetOf<AutopilotRouteRangeDTO>(nameof(AutopilotRouteRangeDTO.Flags)) == 16 &&
                   OffsetOf<AutopilotRouteRangeDTO>(nameof(AutopilotRouteRangeDTO.RouteHash)) == 20 &&
                   OffsetOf<AutopilotRouteRangeDTO>(nameof(AutopilotRouteRangeDTO._pad0)) == 24 &&
                   OffsetOf<AutopilotRouteRangeDTO>(nameof(AutopilotRouteRangeDTO._pad1)) == 28;
        }

        public static bool ValidateTuning()
        {
            return UnsafeUtility.SizeOf<AutopilotTuningDTO>() == 128 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.FeelerLength)) == 0 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.SdfThresholdMeters)) == 4 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.RepulsionWeight)) == 8 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.MaxTurnRateRadians)) == 12 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.WaypointAcceptanceRadius)) == 16 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.FlowCompensationWeight)) == 20 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.TargetSpeedFallback)) == 24 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.GlobalQualityWeight)) == 28 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.SdfOrigin)) == 32 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.SdfCellSize)) == 44 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.SdfDimensions)) == 56 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.SdfRangeMeters)) == 68 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.Flags)) == 72 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.ActiveVehicleCount)) == 76 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.FlowOrigin)) == 80 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.FlowCellSize)) == 92 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.FlowDimensions)) == 104 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.SourceHash)) == 116 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO.ResolvedQualityWeight)) == 120 &&
                   OffsetOf<AutopilotTuningDTO>(nameof(AutopilotTuningDTO._pad1)) == 124;
        }

        public static bool ValidateTelemetry()
        {
            return UnsafeUtility.SizeOf<AutopilotTelemetryEntry>() == 64 &&
                   OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.FirstAUP)) == 0 &&
                   OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.AverageRepulsion)) == 24 &&
                   OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.AverageRepulsionMagnitude)) == 36 &&
                   OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.Frame)) == 40 &&
                   OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.ActiveAutopilots)) == 44 &&
                   OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.FeelerCount)) == 48 &&
                   OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.Flags)) == 52 &&
                   OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.EstimatedBurstMicroseconds)) == 56 &&
                   OffsetOf<AutopilotTelemetryEntry>(nameof(AutopilotTelemetryEntry.StateHash)) == 60;
        }

        public static bool ValidateHandlingProfile()
        {
            return UnsafeUtility.SizeOf<AutopilotHandlingProfileDTO>() == 32 &&
                   OffsetOf<AutopilotHandlingProfileDTO>(nameof(AutopilotHandlingProfileDTO.NameHash)) == 0 &&
                   OffsetOf<AutopilotHandlingProfileDTO>(nameof(AutopilotHandlingProfileDTO.MaxTurnRateRadians)) == 4 &&
                   OffsetOf<AutopilotHandlingProfileDTO>(nameof(AutopilotHandlingProfileDTO.AccelerationLimit)) == 8 &&
                   OffsetOf<AutopilotHandlingProfileDTO>(nameof(AutopilotHandlingProfileDTO.SpeedScale)) == 12 &&
                   OffsetOf<AutopilotHandlingProfileDTO>(nameof(AutopilotHandlingProfileDTO.RepulsionWeight)) == 16 &&
                   OffsetOf<AutopilotHandlingProfileDTO>(nameof(AutopilotHandlingProfileDTO.Flags)) == 20 &&
                   OffsetOf<AutopilotHandlingProfileDTO>(nameof(AutopilotHandlingProfileDTO._pad0)) == 24 &&
                   OffsetOf<AutopilotHandlingProfileDTO>(nameof(AutopilotHandlingProfileDTO._pad1)) == 28;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }
#endif

    public static unsafe class AutopilotStateDTOAccess
    {
        public static ref AutopilotStateDTO ElementAt(AutopilotStateDTO* states, int index)
        {
            return ref UnsafeUtility.AsRef<AutopilotStateDTO>(states + index);
        }

        public static ref readonly AutopilotStateDTO ReadOnlyElementAt(AutopilotStateDTO* states, int index)
        {
            return ref UnsafeUtility.AsRef<AutopilotStateDTO>(states + index);
        }
    }

    internal static class SubmarineAutopilotSimdMath
    {
        private const float TwoPi = 6.28318530718f;
        private const float InvTwoPi = 0.15915494309f;
        private const float HalfPi = 1.57079632679f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LengthFromSq(float lengthSq)
        {
            float finiteSq = math.select(0f, lengthSq, math.isfinite(lengthSq));
            float safeSq = math.max(finiteSq, 0.0001f);
            return math.select(0f, safeSq * math.rsqrt(math.max(safeSq, 0.0001f)), finiteSq > 0.0001f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SinPolynomial7(float radians)
        {
            float safeRadians = math.select(0f, radians, math.isfinite(radians));
            float x = safeRadians - math.floor((safeRadians + math.PI) * InvTwoPi) * TwoPi;
            x = math.select(x, math.PI - x, x > HalfPi);
            x = math.select(x, -math.PI - x, x < -HalfPi);
            float x2 = x * x;
            return x * (1f + x2 * (-0.16666667f + x2 * (0.008333331f + x2 * -0.00019840874f)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosPolynomial7(float radians)
        {
            return SinPolynomial7(radians + HalfPi);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AcosPolynomial(float value)
        {
            float x = math.clamp(math.select(1f, value, math.isfinite(value)), -1f, 1f);
            float ax = math.abs(x);
            float root = LengthFromSq(math.max(0f, 1f - ax));
            float angle = (((-0.0187293f * ax + 0.0742610f) * ax - 0.2121144f) * ax + 1.5707288f) * root;
            return math.select(angle, math.PI - angle, x < 0f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InitializeAutopilotBuffersJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // These raw pointer lanes are generation-checked Vault slices resolved by the autopilot owner before scheduling.
        // The safety system cannot see that shared descriptor proof once the slices are lowered to pointers, so each
        // dereference is guarded by VehicleCapacity and null checks before lane writes.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Rewrapping the lanes as NativeArray fields was rejected because the owner already resolves contiguous pointer
        // views for several jobs in one phase. Duplicating the state into job-local scratch was rejected because it adds
        // a full extra pass and breaks the one-owner Vault route.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is index-exclusive initialization: worker index N reads optional KinematicStates[N] and writes
        // only AutopilotStates[N], Avoidance[N], and RouteRanges[N]; no second writer touches those rows before the
        // returned JobHandle is chained.
        [NoAlias, NativeDisableUnsafePtrRestriction] public SubmarineKinematicState* KinematicStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AutopilotStateDTO* AutopilotStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AutopilotAvoidanceDTO* Avoidance;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AutopilotRouteRangeDTO* RouteRanges;

        public int VehicleCapacity;
        public float TargetSpeedFallback;
        public float AcceptanceRadius;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)VehicleCapacity)
                return;

            double3 aup = double3.zero;
            if (KinematicStates != null)
            {
                ref readonly SubmarineKinematicState state = ref UnsafeUtility.AsRef<SubmarineKinematicState>(KinematicStates + index);
                if (math.all(math.isfinite(state.Aup)))
                    aup = state.Aup;
            }

            AutopilotStateDTO nav = default;
            nav.TargetAUP = aup;
            nav.DesiredVelocity = float3.zero;
            nav.TargetSpeed = SanitizePositive(TargetSpeedFallback, 4f);
            nav.SubmarineHashID = SubmarineAutopilotConstants.HandlingProfileDefaultHash;
            nav.NavFlags = SubmarineAutopilotConstants.NavFlagInitialized;
            AutopilotStates[index] = nav;

            AutopilotAvoidanceDTO avoidance = default;
            avoidance.NearestHitDistance = float.MaxValue;
            Avoidance[index] = avoidance;

            AutopilotRouteRangeDTO route = default;
            route.AcceptanceRadius = SanitizePositive(AcceptanceRadius, 8f);
            RouteRanges[index] = route;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockObstacleSDFJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // EncodedSdf is a single owner-provided byte field used as a deterministic mock SDF payload. Unity cannot track
        // the raw pointer, but each worker validates index < Length and writes exactly one byte at EncodedSdf[index].
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // A managed texture or byte[] fallback was rejected for GC and copy cost. A separate NativeArray mock buffer was
        // rejected because it would create duplicate SDF ownership instead of filling the existing Vault lane.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is one SDF cell per worker index. The owner does not schedule readers over EncodedSdf until this
        // generation job's handle is completed or combined into the downstream avoidance dependency.
        [NoAlias, NativeDisableUnsafePtrRestriction] public byte* EncodedSdf;
        public int Length;
        public int3 Dimensions;
        public float3 Origin;
        public float3 CellSize;
        public float SdfRangeMeters;

        public void Execute(int index)
        {
            if (EncodedSdf == null || (uint)index >= (uint)Length)
                return;

            int width = math.max(1, Dimensions.x);
            int height = math.max(1, Dimensions.y);
            int z = index / (width * height);
            int rem = index - z * width * height;
            int y = rem / width;
            int x = rem - y * width;

            float3 position = Origin + (new float3(x, y, z) + new float3(0.5f)) * CellSize;
            float pillar = 18f - SubmarineAutopilotSimdMath.LengthFromSq(math.lengthsq(new float2(position.x, position.z - 18f)));
            float wall = 4f - math.abs(position.x + 38f);
            float ceiling = position.y - 28f;
            float trenchLip = math.max(wall, math.max(pillar, ceiling));
            float signedDensity = math.clamp(trenchLip, -SdfRangeMeters, SdfRangeMeters);
            EncodedSdf[index] = EncodeSignedSdf(signedDensity, math.max(0.001f, SdfRangeMeters));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte EncodeSignedSdf(float value, float range)
        {
            float normalized = math.saturate((value * math.rcp(range)) * 0.5f + 0.5f);
            return (byte)math.clamp((int)math.round(normalized * 255f), 0, 255);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockFlowFieldJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // FlowSamples is the owner-resolved mock flow lane. Raw pointer use is required for the shared autopilot pointer
        // pipeline, and every write is bounded by Length before FlowSamples[index] is assigned.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Creating a temporary NativeArray flow field was rejected because it adds allocation/fill/copy overhead. Folding
        // flow generation into the avoidance job was rejected because it would duplicate flow math per vehicle instead
        // of once per field sample.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is exclusive field generation: worker index N writes only FlowSamples[N], and downstream jobs can
        // read the lane only through the generated JobHandle dependency.
        [NoAlias, NativeDisableUnsafePtrRestriction] public float3* FlowSamples;
        public int Length;
        public int3 Dimensions;
        public float3 Origin;
        public float3 CellSize;

        public void Execute(int index)
        {
            if (FlowSamples == null || (uint)index >= (uint)Length)
                return;

            int width = math.max(1, Dimensions.x);
            int height = math.max(1, Dimensions.y);
            int z = index / (width * height);
            int rem = index - z * width * height;
            int y = rem / width;
            int x = rem - y * width;
            float3 p = Origin + (new float3(x, y, z) + new float3(0.5f)) * CellSize;
            FlowSamples[index] = new float3(
                SubmarineAutopilotSimdMath.SinPolynomial7(p.z * 0.037f + p.y * 0.011f) * 0.32f,
                SubmarineAutopilotSimdMath.SinPolynomial7(p.x * 0.021f) * 0.035f,
                SubmarineAutopilotSimdMath.CosPolynomial7(p.x * 0.026f - p.y * 0.013f) * 0.26f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateCollisionAvoidanceJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // These pointer lanes are independent Vault slices: kinematics/autopilot state are input/output rows, avoidance
        // and feeler rows are per-vehicle outputs, and EncodedSdf is read-only sample data. The safety system cannot prove
        // that separation after pointer lowering, so the job enforces VehicleCount and SDF length bounds explicitly.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // A Unity physics/MeshCollider path was rejected as CPU-heavy and nondeterministic for the 100km AUP world.
        // Duplicating the SDF into managed or temporary containers was rejected because it creates ownership and copy cost.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is per-vehicle row ownership: Execute(index) writes only the rows derived from index and its
        // bounded feeler range, while EncodedSdf remains immutable until the returned avoidance handle is consumed.
        [NoAlias, NativeDisableUnsafePtrRestriction] public SubmarineKinematicState* KinematicStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AutopilotStateDTO* AutopilotStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AutopilotAvoidanceDTO* Avoidance;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AutopilotFeelerResultDTO* FeelerResults;
        [NoAlias, NativeDisableUnsafePtrRestriction] public byte* EncodedSdf;

        public int VehicleCount;
        public int FeelerResultLength;
        public int EncodedSdfLength;
        public AutopilotTuningDTO Tuning;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)VehicleCount || KinematicStates == null || AutopilotStates == null || Avoidance == null)
                return;

            ref readonly SubmarineKinematicState vehicle = ref UnsafeUtility.AsRef<SubmarineKinematicState>(KinematicStates + index);
            ref readonly AutopilotStateDTO nav = ref AutopilotStateDTOAccess.ReadOnlyElementAt(AutopilotStates, index);
            float quality = ResolveQuality(GlobalQualityWeight);
            int feelerCount = ResolveFeelerCount(quality);
            int stepCount = ResolveStepCount(quality);
            float feelerLength = math.max(1f, SanitizeFinite(Tuning.FeelerLength, 48f));
            float threshold = math.max(0.25f, SanitizeFinite(Tuning.SdfThresholdMeters, 7f));
            float repulsionWeight = math.max(0f, SanitizeFinite(Tuning.RepulsionWeight, 3.5f));
            float3 origin = SanitizeFloat3(vehicle.LocalPosition, float3.zero);
            float3 forward = ResolveForward(vehicle);
            float3 right = ResolveRight(vehicle.Rotation, forward);
            float3 up = NormalizeOrFallback(math.cross(right, forward), new float3(0f, 1f, 0f));
            float3 totalRepulsion = float3.zero;
            float pressureSum = 0f;
            float nearestHitDistance = float.MaxValue;
            uint hitCount = 0u;
            uint flags = SubmarineAutopilotConstants.NavFlagActive | (nav.NavFlags & SubmarineAutopilotConstants.NavFlagInitialized);
            bool hasClearanceEarlyOut = TryResolveSdfClearanceEarlyOut(origin, threshold, out float clearanceDensity);

            int baseFeeler = index * SubmarineAutopilotConstants.MaxFeelersPerVehicle;
            for (int feeler = 0; feeler < SubmarineAutopilotConstants.MaxFeelersPerVehicle; feeler++)
            {
                float3 direction = ResolveFeelerDirection(feeler, feelerCount, forward, right, up);
                AutopilotFeelerResultDTO result = default;
                result.StartRuntime = origin;
                result.EndRuntime = origin + direction * feelerLength;
                result.HitRuntime = result.EndRuntime;
                result.HitDistance = feelerLength;
                result.SdfDensity = -threshold;
                result.FeelerIndex = (uint)feeler;

                if (feeler < feelerCount)
                {
                    result.Flags = SubmarineAutopilotConstants.FeelerFlagActive;
                    float3 repulsion;
                    float hitDistance;
                    float hitDensity;
                    float3 hitPoint;
                    if (hasClearanceEarlyOut)
                    {
                        result.SdfDensity = clearanceDensity;
                    }
                    else if (TryMarchFeeler(
                            origin,
                            direction,
                            feelerLength,
                            threshold,
                            stepCount,
                            out repulsion,
                            out hitDistance,
                            out hitDensity,
                            out hitPoint))
                    {
                        float proximityWeight = 1f - math.saturate(hitDistance * math.rcp(feelerLength));
                        float3 weighted = repulsion * (repulsionWeight * (0.35f + proximityWeight));
                        totalRepulsion += weighted;
                        pressureSum += SubmarineAutopilotSimdMath.LengthFromSq(math.lengthsq(weighted));
                        nearestHitDistance = math.min(nearestHitDistance, hitDistance);
                        hitCount++;
                        result.HitRuntime = hitPoint;
                        result.HitDistance = hitDistance;
                        result.SdfDensity = hitDensity;
                        result.Repulsion = weighted;
                        result.Flags |= SubmarineAutopilotConstants.FeelerFlagHit | SubmarineAutopilotConstants.FeelerFlagGradientValid;
                    }
                    else if (EncodedSdf == null || EncodedSdfLength <= 0)
                    {
                        result.Flags |= SubmarineAutopilotConstants.FeelerFlagSdfMissing;
                    }
                }

                int resultIndex = baseFeeler + feeler;
                if (FeelerResults != null && (uint)resultIndex < (uint)FeelerResultLength)
                    FeelerResults[resultIndex] = result;
            }

            if (EncodedSdf == null || EncodedSdfLength <= 0)
                flags |= SubmarineAutopilotConstants.NavFlagSdfFallback;

            AutopilotAvoidanceDTO avoidance = default;
            avoidance.Repulsion = SanitizeFloat3(totalRepulsion, float3.zero);
            avoidance.Forward = forward;
            avoidance.AverageSdfPressure01 = math.saturate(pressureSum * math.rcp(math.max(1, feelerCount)));
            avoidance.NearestHitDistance = nearestHitDistance;
            avoidance.ActiveFeelerCount = (uint)feelerCount;
            avoidance.HitFeelerCount = hitCount;
            avoidance.Flags = flags;
            Avoidance[index] = avoidance;
        }

        private bool TryResolveSdfClearanceEarlyOut(float3 origin, float threshold, out float density)
        {
            density = -threshold;
            if (EncodedSdf == null || EncodedSdfLength <= 0 || !IsValidSdf(Tuning.SdfDimensions, EncodedSdfLength))
                return false;

            if (!TrySampleSdf(origin, out density) || !math.isfinite(density))
                return false;

            float boundingRadius = math.max(0.25f, threshold);
            return density <= -(boundingRadius * 2f);
        }

        private bool TryMarchFeeler(
            float3 origin,
            float3 direction,
            float length,
            float threshold,
            int stepCount,
            out float3 repulsion,
            out float hitDistance,
            out float hitDensity,
            out float3 hitPoint)
        {
            repulsion = float3.zero;
            hitDistance = length;
            hitDensity = -threshold;
            hitPoint = origin + direction * length;

            if (EncodedSdf == null || EncodedSdfLength <= 0 || !IsValidSdf(Tuning.SdfDimensions, EncodedSdfLength))
                return false;

            float invSteps = math.rcp(math.max(1, stepCount));
            for (int step = 1; step <= stepCount; step++)
            {
                float t = step * invSteps;
                float distance = length * t;
                float3 p = origin + direction * distance;
                if (!TrySampleSdf(p, out float sdf))
                    continue;

                float pressure = math.saturate((threshold + sdf) * math.rcp(math.max(0.001f, threshold)));
                if (pressure <= 0f)
                    continue;

                float3 cheapNormal = -direction;
                float gradientWeight = ResolveSdfGradientWeight(GlobalQualityWeight);
                float3 sampledNormal = cheapNormal;
                if (gradientWeight > 0.0001f)
                    sampledNormal = ResolveOpenNormal(p, direction);
                float3 normal = NormalizeOrFallback(math.lerp(cheapNormal, sampledNormal, gradientWeight), cheapNormal);
                if (math.lengthsq(normal) <= 0.000001f)
                    normal = cheapNormal;

                repulsion = NormalizeOrFallback(normal, -direction) * pressure;
                hitDistance = distance;
                hitDensity = sdf;
                hitPoint = p;
                return true;
            }

            return false;
        }

        private float3 ResolveOpenNormal(float3 p, float3 direction)
        {
            float step = math.max(0.15f, math.cmin(math.abs(Tuning.SdfCellSize)) * 0.5f);
            float3 dx = new float3(step, 0f, 0f);
            float3 dy = new float3(0f, step, 0f);
            float3 dz = new float3(0f, 0f, step);
            if (!TrySampleSdf(p + dx, out float px) ||
                !TrySampleSdf(p - dx, out float nx) ||
                !TrySampleSdf(p + dy, out float py) ||
                !TrySampleSdf(p - dy, out float ny) ||
                !TrySampleSdf(p + dz, out float pz) ||
                !TrySampleSdf(p - dz, out float nz))
            {
                return -direction;
            }

            float3 openGradient = -new float3(px - nx, py - ny, pz - nz);
            if (!math.all(math.isfinite(openGradient)))
                return -direction;

            float3 lateral = openGradient - direction * math.dot(openGradient, direction);
            if (math.lengthsq(lateral) > 0.000001f && math.all(math.isfinite(lateral)))
                openGradient = lateral;

            return NormalizeOrFallback(openGradient, -direction);
        }

        private bool TrySampleSdf(float3 runtimePosition, out float density)
        {
            density = 0f;
            int3 dims = Tuning.SdfDimensions;
            if (EncodedSdf == null || !IsValidSdf(dims, EncodedSdfLength))
                return false;

            float range = math.max(0.001f, SanitizeFinite(Tuning.SdfRangeMeters, 24f));
            float3 invCell = math.rcp(math.max(math.abs(Tuning.SdfCellSize), new float3(0.001f)));
            float3 sample = (runtimePosition - Tuning.SdfOrigin) * invCell;
            float3 minSample = new float3(-0.5f);
            float3 maxSample = new float3(dims.x - 0.5f, dims.y - 0.5f, dims.z - 0.5f);
            if (math.any(sample < minSample) || math.any(sample > maxSample))
                return false;

            sample = math.clamp(sample, float3.zero, new float3(dims.x - 1f, dims.y - 1f, dims.z - 1f));
            int nearestX = math.clamp((int)math.round(sample.x), 0, dims.x - 1);
            int nearestY = math.clamp((int)math.round(sample.y), 0, dims.y - 1);
            int nearestZ = math.clamp((int)math.round(sample.z), 0, dims.z - 1);
            float nearest = DecodeSdfAt(nearestX, nearestY, nearestZ, range);
            float interpolationWeight = ResolveSdfInterpolationWeight(GlobalQualityWeight);
            if (interpolationWeight <= 0.0001f)
            {
                density = nearest;
                return math.isfinite(density);
            }

            int x0 = (int)math.floor(sample.x);
            int y0 = (int)math.floor(sample.y);
            int z0 = (int)math.floor(sample.z);
            int x1 = math.min(x0 + 1, dims.x - 1);
            int y1 = math.min(y0 + 1, dims.y - 1);
            int z1 = math.min(z0 + 1, dims.z - 1);
            float tx = sample.x - x0;
            float ty = sample.y - y0;
            float tz = sample.z - z0;
            float c000 = DecodeSdfAt(x0, y0, z0, range);
            float c100 = DecodeSdfAt(x1, y0, z0, range);
            float c010 = DecodeSdfAt(x0, y1, z0, range);
            float c110 = DecodeSdfAt(x1, y1, z0, range);
            float c001 = DecodeSdfAt(x0, y0, z1, range);
            float c101 = DecodeSdfAt(x1, y0, z1, range);
            float c011 = DecodeSdfAt(x0, y1, z1, range);
            float c111 = DecodeSdfAt(x1, y1, z1, range);
            float c00 = math.lerp(c000, c100, tx);
            float c10 = math.lerp(c010, c110, tx);
            float c01 = math.lerp(c001, c101, tx);
            float c11 = math.lerp(c011, c111, tx);
            float trilinear = math.lerp(math.lerp(c00, c10, ty), math.lerp(c01, c11, ty), tz);
            density = math.lerp(nearest, trilinear, interpolationWeight);
            return math.isfinite(density);
        }

        private float DecodeSdfAt(int x, int y, int z, float range)
        {
            int3 dims = Tuning.SdfDimensions;
            int index = x + dims.x * (y + dims.y * z);
            if ((uint)index >= (uint)EncodedSdfLength)
                return 0f;

            return ((EncodedSdf[index] * 0.0039215686274509803f) * 2f - 1f) * range;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidSdf(int3 dims, int length)
        {
            if (dims.x <= 1 || dims.y <= 1 || dims.z <= 1)
                return false;
            long count = (long)dims.x * dims.y * dims.z;
            return count > 0L && count <= length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveFeelerCount(float quality)
        {
            return math.clamp((int)math.lerp(
                SubmarineAutopilotConstants.MinFeelersPerVehicle,
                SubmarineAutopilotConstants.MaxFeelersPerVehicle,
                quality), SubmarineAutopilotConstants.MinFeelersPerVehicle, SubmarineAutopilotConstants.MaxFeelersPerVehicle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveStepCount(float quality)
        {
            float q = math.saturate(quality);
            return math.clamp((int)math.round(math.lerp(1f, 12f, q * q)), 1, 12);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveSdfInterpolationWeight(float quality)
        {
            return math.smoothstep(0.25f, 0.45f, ResolveQuality(quality));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveSdfGradientWeight(float quality)
        {
            return math.smoothstep(0.30f, 0.55f, ResolveQuality(quality));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveQuality(float value)
        {
            return math.saturate(math.select(1f, value, math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveForward(SubmarineKinematicState vehicle)
        {
            float3 velocity = SanitizeFloat3(vehicle.LinearVelocity, float3.zero);
            if (math.lengthsq(velocity) > 0.0001f)
                return NormalizeOrFallback(velocity, new float3(0f, 0f, 1f));
            return NormalizeOrFallback(math.rotate(vehicle.Rotation, new float3(0f, 0f, 1f)), new float3(0f, 0f, 1f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveRight(quaternion rotation, float3 forward)
        {
            float3 right = math.rotate(rotation, new float3(1f, 0f, 0f));
            if (math.lengthsq(right) <= 0.0001f || !math.all(math.isfinite(right)))
                right = math.cross(new float3(0f, 1f, 0f), forward);
            return NormalizeOrFallback(right, new float3(1f, 0f, 0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveFeelerDirection(int index, int count, float3 forward, float3 right, float3 up)
        {
            if (index <= 0 || count <= 1)
                return forward;

            const float goldenAngle = 2.39996322972865332f;
            float denom = math.max(1f, count - 1f);
            float ring01 = SubmarineAutopilotSimdMath.LengthFromSq(index * math.rcp(denom));
            float angle = index * goldenAngle;
            float lateral = SubmarineAutopilotSimdMath.CosPolynomial7(angle) * (0.18f + ring01 * 0.72f);
            float vertical = SubmarineAutopilotSimdMath.SinPolynomial7(angle) * (0.08f + ring01 * 0.48f);
            return NormalizeOrFallback(forward + right * lateral + up * vertical, forward);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFloat3(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            if (!math.all(math.isfinite(value)) || !math.isfinite(lenSq) || lenSq <= 0.000001f)
                value = fallback;
            lenSq = math.lengthsq(value);
            if (!math.all(math.isfinite(value)) || !math.isfinite(lenSq) || lenSq <= 0.000001f)
                return new float3(0f, 0f, 1f);
            return value * math.rsqrt(math.max(lenSq, 0.000001f));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ComputeDesiredVelocityJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // The desired-velocity job consumes separately owned Vault lanes and writes only AutopilotStates[index]. Unity's
        // raw pointer safety cannot observe the non-overlap contract, so each pointer is paired with explicit count
        // metadata and index guards before dereference.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // A polymorphic route-planner interface was rejected because it would block Burst devirtualization. A broad AoS
        // vehicle DTO was rejected because this job needs only the compact SoA lanes listed here.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is one vehicle row per worker. Waypoint, route, flow, handling, kinematic, and avoidance lanes
        // are read-only for this stage, and only AutopilotStates[index] is mutated under the returned JobHandle.
        [NoAlias, NativeDisableUnsafePtrRestriction] public SubmarineKinematicState* KinematicStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AutopilotStateDTO* AutopilotStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AutopilotAvoidanceDTO* Avoidance;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AutopilotWaypointDTO* Waypoints;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AutopilotRouteRangeDTO* RouteRanges;
        [NoAlias, NativeDisableUnsafePtrRestriction] public float3* FlowSamples;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AutopilotHandlingProfileDTO* HandlingProfiles;

        public int VehicleCount;
        public int WaypointLength;
        public int FlowSampleLength;
        public int HandlingProfileLength;
        public AutopilotTuningDTO Tuning;
        public float DeltaTime;
        public uint Frame;

        private const double MaxAupSteerDeltaMeters = 131072.0d;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)VehicleCount || KinematicStates == null || AutopilotStates == null || Avoidance == null)
                return;

            ref readonly SubmarineKinematicState vehicle = ref UnsafeUtility.AsRef<SubmarineKinematicState>(KinematicStates + index);
            ref AutopilotStateDTO nav = ref AutopilotStateDTOAccess.ElementAt(AutopilotStates, index);
            ref AutopilotAvoidanceDTO avoidance = ref UnsafeUtility.AsRef<AutopilotAvoidanceDTO>(Avoidance + index);
            uint flags = SubmarineAutopilotConstants.NavFlagActive | (nav.NavFlags & SubmarineAutopilotConstants.NavFlagInitialized);
            AutopilotHandlingProfileDTO profile = ResolveHandlingProfile(nav.SubmarineHashID);

            float acceptance = math.max(0.1f, SanitizeFinite(Tuning.WaypointAcceptanceRadius, 8f));
            AdvanceRouteIfNeeded(index, vehicle.Aup, ref nav, ref flags, ref acceptance);

            float targetSpeed = math.max(0f, SanitizeFinite(nav.TargetSpeed, Tuning.TargetSpeedFallback));
            targetSpeed *= math.max(0f, SanitizeFinite(profile.SpeedScale, 1f));
            float3 desired = float3.zero;
            if (!TryResolveLocalTargetDelta(nav.TargetAUP, vehicle.Aup, out float3 localDelta, out float distanceSq))
            {
                flags |= SubmarineAutopilotConstants.NavFlagFatalNaN;
            }
            else if (distanceSq > acceptance * acceptance)
            {
                desired = NormalizeOrFallback(localDelta, avoidance.Forward) * targetSpeed;
            }
            else
            {
                flags |= SubmarineAutopilotConstants.NavFlagAtTarget;
            }

            float profileRepulsion = math.max(0f, SanitizeFinite(profile.RepulsionWeight, 1f));
            float3 repulsion = SanitizeFloat3(avoidance.Repulsion, float3.zero) * profileRepulsion;
            desired += repulsion;

            float3 flow = SampleFlowField(SanitizeFloat3(vehicle.LocalPosition, float3.zero), Frame);
            avoidance.FlowVelocity = flow;
            if (math.lengthsq(flow) > 0.000001f)
                flags |= SubmarineAutopilotConstants.NavFlagFlowCompensated;

            float flowWeight = math.max(0f, SanitizeFinite(Tuning.FlowCompensationWeight, 1f));
            desired -= flow * flowWeight;
            desired = ClampMagnitude(SanitizeFloat3(desired, float3.zero), math.max(targetSpeed, SubmarineAutopilotSimdMath.LengthFromSq(math.lengthsq(repulsion))));
            float turnRate = math.max(0.001f, SanitizeFinite(profile.MaxTurnRateRadians, Tuning.MaxTurnRateRadians));
            desired = ClampTurn(nav.DesiredVelocity, desired, turnRate * math.max(0.0001f, DeltaTime));
            desired = ClampAcceleration(nav.DesiredVelocity, desired, math.max(0f, SanitizeFinite(profile.AccelerationLimit, 0f)) * math.max(0.0001f, DeltaTime));
            desired = SanitizeFloat3(desired, float3.zero);

            if (!math.all(math.isfinite(desired)))
            {
                desired = float3.zero;
                flags |= SubmarineAutopilotConstants.NavFlagFatalNaN;
            }

            nav.DesiredVelocity = desired;
            nav.NavFlags = flags | (avoidance.Flags & SubmarineAutopilotConstants.NavFlagSdfFallback);
            AutopilotStates[index] = nav;
            avoidance.Flags = nav.NavFlags;
            Avoidance[index] = avoidance;
        }

        private void AdvanceRouteIfNeeded(int index, double3 vehicleAup, ref AutopilotStateDTO nav, ref uint flags, ref float acceptance)
        {
            if (RouteRanges == null || Waypoints == null || (uint)index >= (uint)VehicleCount)
                return;

            ref AutopilotRouteRangeDTO route = ref UnsafeUtility.AsRef<AutopilotRouteRangeDTO>(RouteRanges + index);
            if (route.Count <= 0 || route.StartIndex < 0 || route.CurrentOffset < 0)
                return;

            int waypointIndex = route.StartIndex + math.min(route.CurrentOffset, route.Count - 1);
            if ((uint)waypointIndex >= (uint)WaypointLength)
                return;

            float routeAcceptance = route.AcceptanceRadius > 0f ? route.AcceptanceRadius : acceptance;
            acceptance = math.max(0.1f, routeAcceptance);
            ref readonly AutopilotWaypointDTO waypoint = ref UnsafeUtility.AsRef<AutopilotWaypointDTO>(Waypoints + waypointIndex);
            nav.TargetAUP = waypoint.TargetAUP;
            if (waypoint.AcceptanceRadius > 0f)
                acceptance = waypoint.AcceptanceRadius;

            if (!TryResolveDoubleDistanceSq(waypoint.TargetAUP, vehicleAup, out double distanceSq))
                return;
            double acceptSq = (double)acceptance * acceptance;
            if (math.isfinite(distanceSq) && distanceSq <= acceptSq && route.CurrentOffset + 1 < route.Count)
            {
                route.CurrentOffset++;
                int nextIndex = route.StartIndex + route.CurrentOffset;
                if ((uint)nextIndex < (uint)WaypointLength)
                    nav.TargetAUP = Waypoints[nextIndex].TargetAUP;
                flags |= SubmarineAutopilotConstants.NavFlagWaypointAdvanced;
            }

            RouteRanges[index] = route;
        }

        private float3 SampleFlowField(float3 runtimePosition, uint frame)
        {
            if (FlowSamples == null || FlowSampleLength <= 0 || !IsValidGrid(Tuning.FlowDimensions, FlowSampleLength))
                return SampleAnalyticFlow(runtimePosition, frame);

            int3 dims = Tuning.FlowDimensions;
            float3 invCell = math.rcp(math.max(math.abs(Tuning.FlowCellSize), new float3(0.001f)));
            float3 sample = (runtimePosition - Tuning.FlowOrigin) * invCell;
            if (math.any(sample < float3.zero) || math.any(sample > new float3(dims.x - 1f, dims.y - 1f, dims.z - 1f)))
                return SampleAnalyticFlow(runtimePosition, frame);

            sample = math.clamp(sample, float3.zero, new float3(dims.x - 1f, dims.y - 1f, dims.z - 1f));
            int x0 = (int)math.floor(sample.x);
            int y0 = (int)math.floor(sample.y);
            int z0 = (int)math.floor(sample.z);
            float interpolationWeight = ResolveFlowInterpolationWeight(Tuning.ResolvedQualityWeight);
            if (interpolationWeight <= 0.0001f)
            {
                int nx = math.clamp((int)math.round(sample.x), 0, dims.x - 1);
                int ny = math.clamp((int)math.round(sample.y), 0, dims.y - 1);
                int nz = math.clamp((int)math.round(sample.z), 0, dims.z - 1);
                return FlowAt(nx, ny, nz);
            }

            int x1 = math.min(x0 + 1, dims.x - 1);
            int y1 = math.min(y0 + 1, dims.y - 1);
            int z1 = math.min(z0 + 1, dims.z - 1);
            float tx = sample.x - x0;
            float ty = sample.y - y0;
            float tz = sample.z - z0;
            float3 c000 = FlowAt(x0, y0, z0);
            float3 c100 = FlowAt(x1, y0, z0);
            float3 c010 = FlowAt(x0, y1, z0);
            float3 c110 = FlowAt(x1, y1, z0);
            float3 c001 = FlowAt(x0, y0, z1);
            float3 c101 = FlowAt(x1, y0, z1);
            float3 c011 = FlowAt(x0, y1, z1);
            float3 c111 = FlowAt(x1, y1, z1);
            float3 c00 = math.lerp(c000, c100, tx);
            float3 c10 = math.lerp(c010, c110, tx);
            float3 c01 = math.lerp(c001, c101, tx);
            float3 c11 = math.lerp(c011, c111, tx);
            float3 trilinear = math.lerp(math.lerp(c00, c10, ty), math.lerp(c01, c11, ty), tz);
            if (interpolationWeight >= 0.999f)
                return SanitizeFloat3(trilinear, float3.zero);

            int nearestX = math.clamp((int)math.round(sample.x), 0, dims.x - 1);
            int nearestY = math.clamp((int)math.round(sample.y), 0, dims.y - 1);
            int nearestZ = math.clamp((int)math.round(sample.z), 0, dims.z - 1);
            return SanitizeFloat3(math.lerp(FlowAt(nearestX, nearestY, nearestZ), trilinear, interpolationWeight), float3.zero);
        }

        private float3 FlowAt(int x, int y, int z)
        {
            int3 dims = Tuning.FlowDimensions;
            int index = x + dims.x * (y + dims.y * z);
            if ((uint)index >= (uint)FlowSampleLength)
                return float3.zero;
            return SanitizeFloat3(FlowSamples[index], float3.zero);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SampleAnalyticFlow(float3 p, uint frame)
        {
            float phase = frame * 0.013f;
            return new float3(
                SubmarineAutopilotSimdMath.SinPolynomial7(p.z * 0.031f + phase) * 0.24f,
                SubmarineAutopilotSimdMath.SinPolynomial7(p.x * 0.017f + p.z * 0.009f) * 0.035f,
                SubmarineAutopilotSimdMath.CosPolynomial7(p.x * 0.023f - phase) * 0.21f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidGrid(int3 dims, int length)
        {
            if (dims.x <= 1 || dims.y <= 1 || dims.z <= 1)
                return false;
            long count = (long)dims.x * dims.y * dims.z;
            return count > 0L && count <= length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveFlowInterpolationWeight(float quality)
        {
            float q = math.saturate(math.isfinite(quality) ? quality : 1f);
            float t = math.saturate((q - 0.2f) * math.rcp(0.45f));
            return t * t * (3f - 2f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ClampTurn(float3 previous, float3 desired, float maxRadians)
        {
            float previousLen = SubmarineAutopilotSimdMath.LengthFromSq(math.lengthsq(previous));
            float desiredLen = SubmarineAutopilotSimdMath.LengthFromSq(math.lengthsq(desired));
            if (!math.isfinite(previousLen) || previousLen <= 0.0001f || !math.isfinite(desiredLen) || desiredLen <= 0.0001f)
                return desired;

            float3 previousDir = previous * math.rcp(previousLen);
            float3 desiredDir = desired * math.rcp(desiredLen);
            float cos = math.clamp(math.dot(previousDir, desiredDir), -1f, 1f);
            float angle = SubmarineAutopilotSimdMath.AcosPolynomial(cos);
            if (!math.isfinite(angle) || angle <= maxRadians)
                return desired;

            float t = math.saturate(maxRadians * math.rcp(math.max(0.0001f, angle)));
            float3 dir = NormalizeOrFallback(math.lerp(previousDir, desiredDir, t), desiredDir);
            return dir * desiredLen;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ClampAcceleration(float3 previous, float3 desired, float maxDelta)
        {
            if (!math.all(math.isfinite(previous)) || !math.all(math.isfinite(desired)) || !math.isfinite(maxDelta) || maxDelta <= 0.000001f)
                return desired;

            float3 delta = desired - previous;
            float lenSq = math.lengthsq(delta);
            float maxSq = maxDelta * maxDelta;
            if (!math.isfinite(lenSq) || lenSq <= maxSq)
                return desired;

            return previous + delta * (maxDelta * math.rsqrt(math.max(0.000001f, lenSq)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ClampMagnitude(float3 value, float maxMagnitude)
        {
            float lenSq = math.lengthsq(value);
            float maxSq = maxMagnitude * maxMagnitude;
            if (!math.isfinite(lenSq))
                return float3.zero;
            if (maxMagnitude <= 0f)
                return float3.zero;
            if (lenSq <= maxSq)
                return value;
            return value * (maxMagnitude * math.rsqrt(math.max(lenSq, 0.000001f)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFloat3(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            if (!math.all(math.isfinite(value)) || !math.isfinite(lenSq) || lenSq <= 0.000001f)
                value = fallback;
            lenSq = math.lengthsq(value);
            if (!math.all(math.isfinite(value)) || !math.isfinite(lenSq) || lenSq <= 0.000001f)
                return float3.zero;
            return value * math.rsqrt(math.max(lenSq, 0.000001f));
        }

        private AutopilotHandlingProfileDTO ResolveHandlingProfile(uint profileHash)
        {
            AutopilotHandlingProfileDTO profile;
            if (profileHash != 0u && TryFindHandlingProfile(profileHash, out profile))
                return profile;

            if (TryFindHandlingProfile(SubmarineAutopilotConstants.HandlingProfileDefaultHash, out profile))
                return profile;

            profile = default;
            profile.NameHash = SubmarineAutopilotConstants.HandlingProfileDefaultHash;
            profile.MaxTurnRateRadians = math.max(0.001f, SanitizeFinite(Tuning.MaxTurnRateRadians, 0.42f));
            profile.AccelerationLimit = 12f;
            profile.SpeedScale = 1f;
            profile.RepulsionWeight = 1f;
            profile.Flags = SubmarineAutopilotConstants.NavFlagInitialized;
            return profile;
        }

        private bool TryFindHandlingProfile(uint profileHash, out AutopilotHandlingProfileDTO profile)
        {
            profile = default;
            if (HandlingProfiles == null || HandlingProfileLength <= 0 || profileHash == 0u)
                return false;

            int start = (int)(profileHash % (uint)HandlingProfileLength);
            for (int probe = 0; probe < HandlingProfileLength; probe++)
            {
                int index = (start + probe) % HandlingProfileLength;
                AutopilotHandlingProfileDTO candidate = HandlingProfiles[index];
                if (candidate.NameHash == profileHash)
                {
                    profile = SanitizeProfile(candidate);
                    return true;
                }

                if (candidate.NameHash == 0u)
                    return false;
            }

            return false;
        }

        private AutopilotHandlingProfileDTO SanitizeProfile(AutopilotHandlingProfileDTO profile)
        {
            profile.MaxTurnRateRadians = math.isfinite(profile.MaxTurnRateRadians) && profile.MaxTurnRateRadians > 0f
                ? profile.MaxTurnRateRadians
                : math.max(0.001f, SanitizeFinite(Tuning.MaxTurnRateRadians, 0.42f));
            profile.AccelerationLimit = math.isfinite(profile.AccelerationLimit) && profile.AccelerationLimit >= 0f ? profile.AccelerationLimit : 12f;
            profile.SpeedScale = math.isfinite(profile.SpeedScale) && profile.SpeedScale >= 0f ? profile.SpeedScale : 1f;
            profile.RepulsionWeight = math.isfinite(profile.RepulsionWeight) && profile.RepulsionWeight >= 0f ? profile.RepulsionWeight : 1f;
            profile.Flags |= SubmarineAutopilotConstants.NavFlagInitialized;
            return profile;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveLocalTargetDelta(double3 targetAup, double3 originAup, out float3 localDelta, out float distanceSq)
        {
            localDelta = float3.zero;
            distanceSq = 0f;
            double3 delta = targetAup - originAup;
            if (!math.all(math.isfinite(delta)))
                return false;

            double axisMax = math.max(math.max(math.abs(delta.x), math.abs(delta.y)), math.abs(delta.z));
            if (axisMax > MaxAupSteerDeltaMeters)
            {
                double3 scaled = delta * (1.0d / math.max(0.000001d, axisMax));
                double scaledLenSq = math.max(0.000001d, math.dot(scaled, scaled));
                if (!math.isfinite(scaledLenSq))
                    return false;

                double len = axisMax * SubmarineAutopilotSimdMath.LengthFromSq((float)math.min(scaledLenSq, 4.0d));
                delta *= MaxAupSteerDeltaMeters / math.max(0.000001d, len);
            }

            localDelta = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            if (!math.all(math.isfinite(localDelta)))
                return false;

            distanceSq = math.lengthsq(localDelta);
            return math.isfinite(distanceSq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveDoubleDistanceSq(double3 targetAup, double3 originAup, out double distanceSq)
        {
            distanceSq = 0.0d;
            double3 delta = targetAup - originAup;
            if (!math.all(math.isfinite(delta)))
                return false;

            double axisMax = math.max(math.max(math.abs(delta.x), math.abs(delta.y)), math.abs(delta.z));
            if (axisMax > MaxAupSteerDeltaMeters)
            {
                distanceSq = MaxAupSteerDeltaMeters * MaxAupSteerDeltaMeters;
                return true;
            }

            distanceSq = math.dot(delta, delta);
            return math.isfinite(distanceSq);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct RecordAutopilotTelemetryJob : IJob
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Telemetry recording uses raw pointers because the owner resolves a stable black-box ring and cursor from Vault.
        // The job validates required pointers and VehicleCount before writing one ring row and one cursor value.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Managed logging was rejected because it allocates and cannot be replayed deterministically. Per-frame NativeQueue
        // telemetry was rejected because it adds contention and destroys the fixed 300-frame ring contract.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The invariant is single telemetry producer: this job is the only writer to TelemetryRing/TelemetryCursor in the
        // autopilot telemetry phase, and readers consume it after the returned handle is chained by the owner.
        [NoAlias, NativeDisableUnsafePtrRestriction] public SubmarineKinematicState* KinematicStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AutopilotStateDTO* AutopilotStates;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AutopilotAvoidanceDTO* Avoidance;
        [NoAlias, NativeDisableUnsafePtrRestriction] public AutopilotTelemetryEntry* TelemetryRing;
        [NoAlias, NativeDisableUnsafePtrRestriction] public uint* TelemetryCursor;

        public int VehicleCount;
        public uint Frame;
        public float GlobalQualityWeight;

        public void Execute()
        {
            if (TelemetryRing == null || TelemetryCursor == null || AutopilotStates == null || Avoidance == null)
                return;

            uint active = 0u;
            uint flags = 0u;
            uint feelers = 0u;
            float3 repulsion = float3.zero;
            double3 firstAup = double3.zero;
            for (int i = 0; i < VehicleCount; i++)
            {
                ref readonly AutopilotStateDTO nav = ref UnsafeUtility.AsRef<AutopilotStateDTO>(AutopilotStates + i);
                if ((nav.NavFlags & SubmarineAutopilotConstants.NavFlagActive) == 0u)
                    continue;

                active++;
                if (active == 1u && KinematicStates != null)
                {
                    double3 candidateAup = UnsafeUtility.AsRef<SubmarineKinematicState>(KinematicStates + i).Aup;
                    if (math.all(math.isfinite(candidateAup)))
                        firstAup = candidateAup;
                    else
                        flags |= SubmarineAutopilotConstants.NavFlagFatalNaN;
                }

                ref readonly AutopilotAvoidanceDTO avoidance = ref UnsafeUtility.AsRef<AutopilotAvoidanceDTO>(Avoidance + i);
                repulsion += avoidance.Repulsion;
                feelers += avoidance.ActiveFeelerCount;
                flags |= nav.NavFlags;
                if (!math.all(math.isfinite(nav.DesiredVelocity)))
                    flags |= SubmarineAutopilotConstants.NavFlagFatalNaN;
            }

            if (!math.all(math.isfinite(repulsion)))
            {
                repulsion = float3.zero;
                flags |= SubmarineAutopilotConstants.NavFlagFatalNaN;
            }

            float invActive = active > 0u ? math.rcp((float)active) : 0f;
            float3 avgRepulsion = repulsion * invActive;
            if (!math.all(math.isfinite(avgRepulsion)))
            {
                avgRepulsion = float3.zero;
                flags |= SubmarineAutopilotConstants.NavFlagFatalNaN;
            }

            uint resolvedFeelers = active > 0u ? feelers / active : 0u;
            float quality = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            int stepCount = math.clamp((int)math.round(math.lerp(1f, 12f, quality * quality)), 1, 12);
            float estimatedUs = active * math.max(1u, resolvedFeelers) * stepCount * 0.42f;
            if (estimatedUs > 1000f)
                flags |= SubmarineAutopilotConstants.NavFlagSlowBurst;

            int cursor = (int)(Frame % SubmarineAutopilotConstants.BlackBoxFrames);
            AutopilotTelemetryEntry entry = default;
            entry.FirstAUP = firstAup;
            entry.AverageRepulsion = avgRepulsion;
            entry.AverageRepulsionMagnitude = SubmarineAutopilotSimdMath.LengthFromSq(math.lengthsq(avgRepulsion));
            entry.Frame = Frame;
            entry.ActiveAutopilots = active;
            entry.FeelerCount = resolvedFeelers;
            entry.Flags = flags;
            entry.EstimatedBurstMicroseconds = estimatedUs;
            entry.StateHash = HashTelemetry(entry);
            TelemetryRing[cursor] = entry;
            TelemetryCursor[0] = (uint)(cursor + 1);
        }

        private static uint HashTelemetry(AutopilotTelemetryEntry entry)
        {
            uint hash = 2166136261u;
            hash = (hash ^ entry.Frame) * 16777619u;
            hash = (hash ^ entry.ActiveAutopilots) * 16777619u;
            hash = (hash ^ entry.FeelerCount) * 16777619u;
            hash = (hash ^ entry.Flags) * 16777619u;
            return hash;
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physics/Vehicles/Submarine Autopilot SDF Navigator")]
    public unsafe sealed class SubmarineAutopilotSdfNavigator : MonoBehaviour, IFixedTickable, IPostFixedTickable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const string AgentDumpFileName = "Dump_SHINOBU_157.bin";
        private const string NavigationSurgeonDumpFileName = "Dump_NAVIGATION_SURGEON.bin";
        private const long MaxCsvBytes = SubmarineAutopilotConstants.CsvScratchBytes;
        private const uint LockKinematicStates = 1u << 0;
        private const uint LockAutopilotStates = 1u << 1;
        private const uint LockAutopilotAvoidance = 1u << 2;
        private const uint LockFeelerResults = 1u << 3;
        private const uint LockWaypoints = 1u << 4;
        private const uint LockRouteRanges = 1u << 5;
        private const uint LockTuning = 1u << 6;
        private const uint LockTelemetryRing = 1u << 7;
        private const uint LockTelemetryCursor = 1u << 8;
        private const uint LockMockSdf = 1u << 9;
        private const uint LockFlowSamples = 1u << 10;
        private const uint LockHandlingProfiles = 1u << 11;

        [SerializeField, Range(1, SubmarineAutopilotConstants.MaxVehicles)] private int vehicleCapacity = 1;
        [SerializeField, Min(1f)] private float defaultTargetSpeed = 8f;
        [SerializeField, Min(1f)] private float feelerLength = 72f;
        [SerializeField, Min(0.1f)] private float sdfThresholdMeters = 9f;
        [SerializeField, Min(0f)] private float repulsionWeight = 4.5f;
        [SerializeField, Min(0.001f)] private float maxTurnRateRadians = 0.42f;
        [SerializeField, Min(0.1f)] private float waypointAcceptanceRadius = 10f;
        [SerializeField, Min(0f)] private float flowCompensationWeight = 1f;
        [SerializeField] private bool drawFeelerGizmos = true;

        private IDataVault _dataVault;
        private VaultGenerationHandle<SubmarineKinematicState> _kinematicHandle;
        private VaultGenerationHandle<AutopilotStateDTO> _autopilotHandle;
        private VaultGenerationHandle<AutopilotAvoidanceDTO> _avoidanceHandle;
        private VaultGenerationHandle<AutopilotFeelerResultDTO> _feelerHandle;
        private VaultGenerationHandle<AutopilotWaypointDTO> _waypointHandle;
        private VaultGenerationHandle<AutopilotRouteRangeDTO> _routeHandle;
        private VaultGenerationHandle<AutopilotTuningDTO> _tuningHandle;
        private VaultGenerationHandle<AutopilotTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<uint> _telemetryCursorHandle;
        private VaultGenerationHandle<byte> _mockSdfHandle;
        private VaultGenerationHandle<float3> _flowHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<AutopilotHandlingProfileDTO> _handlingProfileHandle;

        private JobHandle _solverHandle;
        private JobHandle _initHandle;
        private bool _solverPending;
        private bool _initPending;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredSlow;
        private bool _registeredHotSwap;
        private bool _buffersReady;
        private bool _buffersLocked;
        private bool _initialized;
        private bool _faulted;
        private bool _dumped;
        private uint _lockMask;
        private int _resolvedVehicleCapacity;
        private float _accumulatedSolverDeltaTime;
        private uint _frame;
        private uint _fixedFrame;
        private string _projectRoot;
        private string _csvPath;
        private long _csvLastWriteTicks;

        public static bool TryGetLatest(out SubmarineAutopilotSdfNavigator navigator)
        {
            navigator = _latest;
            return navigator != null && navigator._buffersReady;
        }

        private static SubmarineAutopilotSdfNavigator _latest;

        private void OnEnable()
        {
            _latest = this;
            _projectRoot = ResolveProjectRoot();
            _csvPath = ResolveHandlingProfilesCsvPath(_projectRoot);
            EnsureDataVault();
            EnsureVaultBuffers();
            _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
            _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void OnDisable()
        {
            CompletePendingJobsForTeardown();
            UnlockBuffers();
            DumpBlackBoxIfFaulted();
            ReleaseAutopilotVaultHandles(_dataVault);
            if (_registeredFixed)
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            if (_registeredPostFixed)
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
            if (_registeredSlow)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            if (_registeredHotSwap)
                GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredFixed = false;
            _registeredPostFixed = false;
            _registeredSlow = false;
            _registeredHotSwap = false;
            if (ReferenceEquals(_latest, this))
                _latest = null;
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault previousVault = previousService as IDataVault;
            CompletePendingJobsForTeardown();
            UnlockBuffers();
            DumpBlackBoxIfFaulted();
            ReleaseAutopilotVaultHandles(previousVault ?? _dataVault);
            _dataVault = currentService as IDataVault;
            if (_dataVault != null)
                EnsureVaultBuffers();
        }

        public void FixedTick(float fixedDeltaTime)
        {
            float safeDeltaTime = SanitizeFixedDeltaTime(fixedDeltaTime);
            if (_solverPending || _initPending)
            {
                _accumulatedSolverDeltaTime = math.min(0.25f, _accumulatedSolverDeltaTime + safeDeltaTime);
                return;
            }

            if (!EnsureVaultBuffers())
                return;

            if (!_initialized)
            {
                ScheduleInitialization();
                return;
            }

            _accumulatedSolverDeltaTime = math.min(0.25f, _accumulatedSolverDeltaTime + safeDeltaTime);
            uint tick = _fixedFrame++;
            float quality = ResolveSchedulingQualityWeight();
            int cadenceFrames = ResolveSolverCadenceFrames(quality);
            if (cadenceFrames > 1 && tick % (uint)cadenceFrames != 0u)
                return;

            float solverDeltaTime = math.max(0.0001f, _accumulatedSolverDeltaTime);
            if (ScheduleSolver(solverDeltaTime))
                _accumulatedSolverDeltaTime = 0f;
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            TryFinalizePendingJobsNoWait();
        }

        public void SlowTick()
        {
            if (!_buffersReady || _buffersLocked || _solverPending || _initPending)
                return;

#if UNITY_EDITOR
            TryApplyHandlingProfilesCsv();
#endif
        }

        public bool TryWriteTargetAup(int submarineIndex, double3 targetAup, float speed)
        {
            if (!_buffersReady || _dataVault == null || _buffersLocked || (uint)submarineIndex >= (uint)vehicleCapacity || _solverPending || _initPending)
                return false;

            if (!_dataVault.TryLockBuffer(SubmarineAutopilotVaultRoute.AutopilotStates, SystemID.VehiclesPhysics))
                return false;

            try
            {
                if (!TryResolveAutopilotVaultBuffer(
                        _dataVault,
                        in _autopilotHandle,
                        SubmarineAutopilotVaultRoute.AutopilotStates,
                        math.clamp(vehicleCapacity, 1, SubmarineAutopilotConstants.MaxVehicles),
                        out NativeArray<AutopilotStateDTO> states))
                    return false;

                AutopilotStateDTO state = states[submarineIndex];
                state.TargetAUP = targetAup;
                state.TargetSpeed = math.max(0f, math.isfinite(speed) && speed > 0f ? speed : state.TargetSpeed);
                state.NavFlags |= SubmarineAutopilotConstants.NavFlagActive | SubmarineAutopilotConstants.NavFlagInitialized;
                states[submarineIndex] = state;
                return true;
            }
            finally
            {
                _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotStates, SystemID.VehiclesPhysics);
            }
        }

        /// <summary>
        /// Writes the deterministic handling profile hash consumed by the Burst steering job.
        /// </summary>
        public bool TryWriteHandlingProfileHash(int submarineIndex, uint profileHash)
        {
            if (!_buffersReady || _dataVault == null || _buffersLocked || profileHash == 0u || (uint)submarineIndex >= (uint)vehicleCapacity || _solverPending || _initPending)
                return false;

            if (!_dataVault.TryLockBuffer(SubmarineAutopilotVaultRoute.AutopilotStates, SystemID.VehiclesPhysics))
                return false;

            try
            {
                if (!TryResolveAutopilotVaultBuffer(
                        _dataVault,
                        in _autopilotHandle,
                        SubmarineAutopilotVaultRoute.AutopilotStates,
                        math.clamp(vehicleCapacity, 1, SubmarineAutopilotConstants.MaxVehicles),
                        out NativeArray<AutopilotStateDTO> states))
                    return false;

                AutopilotStateDTO state = states[submarineIndex];
                state.SubmarineHashID = profileHash;
                state.NavFlags |= SubmarineAutopilotConstants.NavFlagInitialized;
                states[submarineIndex] = state;
                return true;
            }
            finally
            {
                _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotStates, SystemID.VehiclesPhysics);
            }
        }

        public bool TryWriteRoute(int submarineIndex, ReadOnlySpan<AutopilotWaypointDTO> waypoints, float acceptanceRadius, uint routeHash)
        {
            if (!_buffersReady || _dataVault == null || _buffersLocked || _solverPending || _initPending || waypoints.Length <= 0)
                return false;

            int resolvedCapacity = _resolvedVehicleCapacity > 0 ? _resolvedVehicleCapacity : vehicleCapacity;
            int capacity = math.clamp(resolvedCapacity, 1, SubmarineAutopilotConstants.MaxVehicles);
            if ((uint)submarineIndex >= (uint)capacity)
                return false;

            int slotsPerSubmarine = math.max(1, SubmarineAutopilotConstants.WaypointCapacity / capacity);
            int startIndex = submarineIndex * slotsPerSubmarine;
            int count = math.min(waypoints.Length, slotsPerSubmarine);
            if (count <= 0 || startIndex < 0 || startIndex + count > SubmarineAutopilotConstants.WaypointCapacity)
                return false;

            float fallbackAcceptance = math.isfinite(waypointAcceptanceRadius) && waypointAcceptanceRadius > 0f ? waypointAcceptanceRadius : 10f;
            float safeAcceptance = math.max(0.1f, math.isfinite(acceptanceRadius) && acceptanceRadius > 0f ? acceptanceRadius : fallbackAcceptance);
            for (int i = 0; i < count; i++)
            {
                if (!math.all(math.isfinite(waypoints[i].TargetAUP)))
                    return false;
            }

            bool waypointLocked = false;
            bool routeLocked = false;
            bool stateLocked = false;
            if (!_dataVault.TryLockBuffer(SubmarineAutopilotVaultRoute.AutopilotWaypoints, SystemID.VehiclesPhysics))
                return false;

            waypointLocked = true;
            try
            {
                routeLocked = _dataVault.TryLockBuffer(SubmarineAutopilotVaultRoute.AutopilotRouteRanges, SystemID.VehiclesPhysics);
                if (!routeLocked)
                    return false;

                stateLocked = _dataVault.TryLockBuffer(SubmarineAutopilotVaultRoute.AutopilotStates, SystemID.VehiclesPhysics);
                if (!stateLocked)
                    return false;

                if (!TryResolveAutopilotVaultBuffer(_dataVault, in _waypointHandle, SubmarineAutopilotVaultRoute.AutopilotWaypoints, SubmarineAutopilotConstants.WaypointCapacity, out NativeArray<AutopilotWaypointDTO> waypointBuffer) ||
                    !TryResolveAutopilotVaultBuffer(_dataVault, in _routeHandle, SubmarineAutopilotVaultRoute.AutopilotRouteRanges, capacity, out NativeArray<AutopilotRouteRangeDTO> routeBuffer) ||
                    !TryResolveAutopilotVaultBuffer(_dataVault, in _autopilotHandle, SubmarineAutopilotVaultRoute.AutopilotStates, capacity, out NativeArray<AutopilotStateDTO> stateBuffer))
                    return false;

                uint resolvedHash = routeHash != 0u ? routeHash : HashRouteHeader(submarineIndex, count);
                for (int i = 0; i < count; i++)
                {
                    AutopilotWaypointDTO waypoint = waypoints[i];
                    waypoint.AcceptanceRadius = math.max(0.1f, math.isfinite(waypoint.AcceptanceRadius) && waypoint.AcceptanceRadius > 0f ? waypoint.AcceptanceRadius : safeAcceptance);
                    waypoint.Flags |= SubmarineAutopilotConstants.WaypointFlagActive;
                    waypointBuffer[startIndex + i] = waypoint;
                }

                routeBuffer[submarineIndex] = new AutopilotRouteRangeDTO
                {
                    StartIndex = startIndex,
                    Count = count,
                    CurrentOffset = 0,
                    AcceptanceRadius = safeAcceptance,
                    Flags = SubmarineAutopilotConstants.RouteFlagActive,
                    RouteHash = resolvedHash
                };

                AutopilotStateDTO state = stateBuffer[submarineIndex];
                state.TargetAUP = waypointBuffer[startIndex].TargetAUP;
                state.NavFlags |= SubmarineAutopilotConstants.NavFlagActive | SubmarineAutopilotConstants.NavFlagInitialized;
                stateBuffer[submarineIndex] = state;
                return true;
            }
            finally
            {
                if (stateLocked)
                    _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotStates, SystemID.VehiclesPhysics);
                if (routeLocked)
                    _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotRouteRanges, SystemID.VehiclesPhysics);
                if (waypointLocked)
                    _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotWaypoints, SystemID.VehiclesPhysics);
            }
        }

        public bool TryReadTuning(out AutopilotTuningDTO tuning)
        {
            tuning = default;
            if (!_buffersReady || _dataVault == null || _buffersLocked || _solverPending || _initPending)
                return false;

            if (!TryReadAutopilotVaultBuffer(_dataVault, in _tuningHandle, SubmarineAutopilotVaultRoute.AutopilotTuning, 1, out NativeArray<AutopilotTuningDTO> tuningBuffer))
                return false;

            tuning = SanitizeTuning(tuningBuffer[0]);
            tuning.ResolvedQualityWeight = ResolveRuntimeQualityWeight(tuning.GlobalQualityWeight);
            return true;
        }

        public bool TryReadAutopilotState(int index, out AutopilotStateDTO state)
        {
            state = default;
            if (!_buffersReady || _dataVault == null || _buffersLocked || _solverPending || _initPending || (uint)index >= (uint)vehicleCapacity)
                return false;

            if (!TryReadAutopilotVaultBuffer(
                    _dataVault,
                    in _autopilotHandle,
                    SubmarineAutopilotVaultRoute.AutopilotStates,
                    math.clamp(vehicleCapacity, 1, SubmarineAutopilotConstants.MaxVehicles),
                    out NativeArray<AutopilotStateDTO> states))
                return false;

            state = states[index];
            return true;
        }

        public bool TryReadLatestTelemetry(out AutopilotTelemetryEntry telemetry)
        {
            telemetry = default;
            if (!_buffersReady || _dataVault == null || _buffersLocked || _solverPending || _initPending)
                return false;

            if (!TryReadAutopilotVaultBuffer(_dataVault, in _telemetryHandle, SubmarineAutopilotVaultRoute.AutopilotTelemetryRing, SubmarineAutopilotConstants.BlackBoxFrames, out NativeArray<AutopilotTelemetryEntry> ring) ||
                !TryReadAutopilotVaultBuffer(_dataVault, in _telemetryCursorHandle, SubmarineAutopilotVaultRoute.AutopilotTelemetryCursor, 1, out NativeArray<uint> cursor))
                return false;
            uint cursorValue = cursor[0];
            if (cursorValue == 0u)
                return false;

            int latest = ((int)cursorValue - 1 + SubmarineAutopilotConstants.BlackBoxFrames) % SubmarineAutopilotConstants.BlackBoxFrames;
            telemetry = ring[latest];
            return true;
        }

        public bool TryWriteTuning(in AutopilotTuningDTO tuning)
        {
            if (!_buffersReady || _dataVault == null || _buffersLocked || _solverPending || _initPending)
                return false;

            if (!_dataVault.TryLockBuffer(SubmarineAutopilotVaultRoute.AutopilotTuning, SystemID.VehiclesPhysics))
                return false;

            try
            {
                if (!TryResolveAutopilotVaultBuffer(_dataVault, in _tuningHandle, SubmarineAutopilotVaultRoute.AutopilotTuning, 1, out NativeArray<AutopilotTuningDTO> tuningBuffer))
                    return false;

                AutopilotTuningDTO sanitized = SanitizeTuning(tuning);
                sanitized.ResolvedQualityWeight = ResolveRuntimeQualityWeight(sanitized.GlobalQualityWeight);
                tuningBuffer[0] = sanitized;
                return true;
            }
            finally
            {
                _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotTuning, SystemID.VehiclesPhysics);
            }
        }

        private bool EnsureDataVault()
        {
            if (_dataVault != null)
                return true;

            _dataVault = GlobalRegistry.DataVault;

            return _dataVault != null;
        }

        private bool EnsureVaultBuffers()
        {
            if (!EnsureDataVault())
                return false;

            int capacity = math.clamp(vehicleCapacity, 1, SubmarineAutopilotConstants.MaxVehicles);
            if (_buffersReady && _resolvedVehicleCapacity == capacity && AreVaultHandlesReady(capacity))
                return true;

            if (_dataVault.IsCompactionFenceActive || _dataVault.IsAllocationLocked)
                return false;

            if (_initialized && _resolvedVehicleCapacity != 0 && _resolvedVehicleCapacity != capacity)
                _initialized = false;

            ReleaseAutopilotVaultHandles(_dataVault);

            if (!_dataVault.TryGetGenerationHandle(BufferID.SubmarineKinematicStates, out _kinematicHandle) ||
                !HasAutopilotVaultBuffer(_dataVault, in _kinematicHandle, BufferID.SubmarineKinematicStates, capacity))
            {
                _kinematicHandle = default;
                _buffersReady = false;
                return false;
            }

            _autopilotHandle = _dataVault.EnsureGenerationHandle<AutopilotStateDTO>(
                SubmarineAutopilotVaultRoute.AutopilotStates,
                capacity,
                SystemID.VehiclesPhysics,
                NativeArrayOptions.UninitializedMemory);
            _avoidanceHandle = _dataVault.EnsureGenerationHandle<AutopilotAvoidanceDTO>(
                SubmarineAutopilotVaultRoute.AutopilotAvoidance,
                capacity,
                SystemID.VehiclesPhysics,
                NativeArrayOptions.UninitializedMemory);
            _feelerHandle = _dataVault.EnsureGenerationHandle<AutopilotFeelerResultDTO>(
                SubmarineAutopilotVaultRoute.AutopilotFeelerResults,
                capacity * SubmarineAutopilotConstants.MaxFeelersPerVehicle,
                SystemID.VehiclesPhysics,
                NativeArrayOptions.UninitializedMemory);
            _waypointHandle = _dataVault.EnsureGenerationHandle<AutopilotWaypointDTO>(
                SubmarineAutopilotVaultRoute.AutopilotWaypoints,
                SubmarineAutopilotConstants.WaypointCapacity,
                SystemID.VehiclesPhysics,
                NativeArrayOptions.UninitializedMemory);
            _routeHandle = _dataVault.EnsureGenerationHandle<AutopilotRouteRangeDTO>(
                SubmarineAutopilotVaultRoute.AutopilotRouteRanges,
                capacity,
                SystemID.VehiclesPhysics,
                NativeArrayOptions.UninitializedMemory);
            _tuningHandle = _dataVault.EnsureGenerationHandle<AutopilotTuningDTO>(
                SubmarineAutopilotVaultRoute.AutopilotTuning,
                1,
                SystemID.VehiclesPhysics,
                NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = _dataVault.EnsureGenerationHandle<AutopilotTelemetryEntry>(
                SubmarineAutopilotVaultRoute.AutopilotTelemetryRing,
                SubmarineAutopilotConstants.BlackBoxFrames,
                SystemID.VehiclesPhysics,
                NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = _dataVault.EnsureGenerationHandle<uint>(
                SubmarineAutopilotVaultRoute.AutopilotTelemetryCursor,
                1,
                SystemID.VehiclesPhysics,
                NativeArrayOptions.UninitializedMemory);
            _mockSdfHandle = _dataVault.EnsureGenerationHandle<byte>(
                SubmarineAutopilotVaultRoute.AutopilotMockSdf,
                SubmarineAutopilotConstants.MockSdfVoxelCount,
                SystemID.VehiclesPhysics,
                NativeArrayOptions.UninitializedMemory);
            _flowHandle = _dataVault.EnsureGenerationHandle<float3>(
                SubmarineAutopilotVaultRoute.AutopilotFlowSamples,
                SubmarineAutopilotConstants.FlowSampleCount,
                SystemID.VehiclesPhysics,
                NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = _dataVault.EnsureGenerationHandle<byte>(
                SubmarineAutopilotVaultRoute.AutopilotCsvScratch,
                SubmarineAutopilotConstants.CsvScratchBytes,
                SystemID.VehiclesPhysics,
                NativeArrayOptions.UninitializedMemory);
            _handlingProfileHandle = _dataVault.EnsureGenerationHandle<AutopilotHandlingProfileDTO>(
                SubmarineAutopilotVaultRoute.AutopilotHandlingProfiles,
                SubmarineAutopilotConstants.HandlingProfileCapacity,
                SystemID.VehiclesPhysics,
                NativeArrayOptions.UninitializedMemory);

            _buffersReady = AreVaultHandlesReady(capacity);

            if (_buffersReady)
            {
                _resolvedVehicleCapacity = capacity;
                if (!_initialized)
                    WriteColdDefaults();
                return true;
            }

            ReleaseAutopilotVaultHandles(_dataVault);
            return false;
        }

        private bool AreVaultHandlesReady(int capacity)
        {
            return
                HasAutopilotVaultBuffer(_dataVault, in _kinematicHandle, BufferID.SubmarineKinematicStates, capacity) &&
                HasAutopilotVaultBuffer(_dataVault, in _autopilotHandle, SubmarineAutopilotVaultRoute.AutopilotStates, capacity) &&
                HasAutopilotVaultBuffer(_dataVault, in _avoidanceHandle, SubmarineAutopilotVaultRoute.AutopilotAvoidance, capacity) &&
                HasAutopilotVaultBuffer(_dataVault, in _feelerHandle, SubmarineAutopilotVaultRoute.AutopilotFeelerResults, capacity * SubmarineAutopilotConstants.MaxFeelersPerVehicle) &&
                HasAutopilotVaultBuffer(_dataVault, in _waypointHandle, SubmarineAutopilotVaultRoute.AutopilotWaypoints, SubmarineAutopilotConstants.WaypointCapacity) &&
                HasAutopilotVaultBuffer(_dataVault, in _routeHandle, SubmarineAutopilotVaultRoute.AutopilotRouteRanges, capacity) &&
                HasAutopilotVaultBuffer(_dataVault, in _tuningHandle, SubmarineAutopilotVaultRoute.AutopilotTuning, 1) &&
                HasAutopilotVaultBuffer(_dataVault, in _telemetryHandle, SubmarineAutopilotVaultRoute.AutopilotTelemetryRing, SubmarineAutopilotConstants.BlackBoxFrames) &&
                HasAutopilotVaultBuffer(_dataVault, in _telemetryCursorHandle, SubmarineAutopilotVaultRoute.AutopilotTelemetryCursor, 1) &&
                HasAutopilotVaultBuffer(_dataVault, in _mockSdfHandle, SubmarineAutopilotVaultRoute.AutopilotMockSdf, SubmarineAutopilotConstants.MockSdfVoxelCount) &&
                HasAutopilotVaultBuffer(_dataVault, in _flowHandle, SubmarineAutopilotVaultRoute.AutopilotFlowSamples, SubmarineAutopilotConstants.FlowSampleCount) &&
                HasAutopilotVaultBuffer(_dataVault, in _csvScratchHandle, SubmarineAutopilotVaultRoute.AutopilotCsvScratch, SubmarineAutopilotConstants.CsvScratchBytes) &&
                HasAutopilotVaultBuffer(_dataVault, in _handlingProfileHandle, SubmarineAutopilotVaultRoute.AutopilotHandlingProfiles, SubmarineAutopilotConstants.HandlingProfileCapacity);
        }

        private static bool HasAutopilotVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength)
            where T : struct
        {
            return TryReadAutopilotVaultBuffer(vault, in handle, bufferId, requiredLength, out _);
        }

        private static bool TryResolveAutopilotVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsAutopilotVaultHandle(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryReadAutopilotVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsAutopilotVaultHandle(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsAutopilotVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)SystemID.VehiclesPhysics &&
                   handle.Generation != 0u;
        }

        private void ReleaseAutopilotVaultHandles(IDataVault vault)
        {
            ReleaseOwnedAutopilotVaultHandle(vault, ref _autopilotHandle, SubmarineAutopilotVaultRoute.AutopilotStates);
            ReleaseOwnedAutopilotVaultHandle(vault, ref _avoidanceHandle, SubmarineAutopilotVaultRoute.AutopilotAvoidance);
            ReleaseOwnedAutopilotVaultHandle(vault, ref _feelerHandle, SubmarineAutopilotVaultRoute.AutopilotFeelerResults);
            ReleaseOwnedAutopilotVaultHandle(vault, ref _waypointHandle, SubmarineAutopilotVaultRoute.AutopilotWaypoints);
            ReleaseOwnedAutopilotVaultHandle(vault, ref _routeHandle, SubmarineAutopilotVaultRoute.AutopilotRouteRanges);
            ReleaseOwnedAutopilotVaultHandle(vault, ref _tuningHandle, SubmarineAutopilotVaultRoute.AutopilotTuning);
            ReleaseOwnedAutopilotVaultHandle(vault, ref _telemetryHandle, SubmarineAutopilotVaultRoute.AutopilotTelemetryRing);
            ReleaseOwnedAutopilotVaultHandle(vault, ref _telemetryCursorHandle, SubmarineAutopilotVaultRoute.AutopilotTelemetryCursor);
            ReleaseOwnedAutopilotVaultHandle(vault, ref _mockSdfHandle, SubmarineAutopilotVaultRoute.AutopilotMockSdf);
            ReleaseOwnedAutopilotVaultHandle(vault, ref _flowHandle, SubmarineAutopilotVaultRoute.AutopilotFlowSamples);
            ReleaseOwnedAutopilotVaultHandle(vault, ref _csvScratchHandle, SubmarineAutopilotVaultRoute.AutopilotCsvScratch);
            ReleaseOwnedAutopilotVaultHandle(vault, ref _handlingProfileHandle, SubmarineAutopilotVaultRoute.AutopilotHandlingProfiles);

            _kinematicHandle = default;
            _buffersReady = false;
            _initialized = false;
            _resolvedVehicleCapacity = 0;
        }

        private static void ReleaseOwnedAutopilotVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : struct
        {
            if (vault != null && IsAutopilotVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void WriteColdDefaults()
        {
            if (TryResolveAutopilotVaultBuffer(_dataVault, in _tuningHandle, SubmarineAutopilotVaultRoute.AutopilotTuning, 1, out NativeArray<AutopilotTuningDTO> tuning))
                tuning[0] = BuildDefaultTuning();

            if (TryResolveAutopilotVaultBuffer(_dataVault, in _telemetryCursorHandle, SubmarineAutopilotVaultRoute.AutopilotTelemetryCursor, 1, out NativeArray<uint> cursor))
                cursor[0] = 0u;

            if (TryResolveAutopilotVaultBuffer(_dataVault, in _handlingProfileHandle, SubmarineAutopilotVaultRoute.AutopilotHandlingProfiles, SubmarineAutopilotConstants.HandlingProfileCapacity, out NativeArray<AutopilotHandlingProfileDTO> profileBuffer))
            {
                AutopilotHandlingProfileDTO* profiles = (AutopilotHandlingProfileDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(profileBuffer);
                WriteDefaultHandlingProfiles(profiles, profileBuffer.Length);
            }
        }

        private void ScheduleInitialization()
        {
            if (!LockInitializationBuffers())
                return;

            int capacity = math.clamp(vehicleCapacity, 1, SubmarineAutopilotConstants.MaxVehicles);
            if (!TryResolveAutopilotVaultBuffer(_dataVault, in _kinematicHandle, BufferID.SubmarineKinematicStates, capacity, out NativeArray<SubmarineKinematicState> kinematicBuffer) ||
                !TryResolveAutopilotVaultBuffer(_dataVault, in _autopilotHandle, SubmarineAutopilotVaultRoute.AutopilotStates, capacity, out NativeArray<AutopilotStateDTO> stateBuffer) ||
                !TryResolveAutopilotVaultBuffer(_dataVault, in _avoidanceHandle, SubmarineAutopilotVaultRoute.AutopilotAvoidance, capacity, out NativeArray<AutopilotAvoidanceDTO> avoidanceBuffer) ||
                !TryResolveAutopilotVaultBuffer(_dataVault, in _routeHandle, SubmarineAutopilotVaultRoute.AutopilotRouteRanges, capacity, out NativeArray<AutopilotRouteRangeDTO> routeBuffer) ||
                !TryResolveAutopilotVaultBuffer(_dataVault, in _mockSdfHandle, SubmarineAutopilotVaultRoute.AutopilotMockSdf, SubmarineAutopilotConstants.MockSdfVoxelCount, out NativeArray<byte> sdfBuffer) ||
                !TryResolveAutopilotVaultBuffer(_dataVault, in _flowHandle, SubmarineAutopilotVaultRoute.AutopilotFlowSamples, SubmarineAutopilotConstants.FlowSampleCount, out NativeArray<float3> flowBuffer))
            {
                UnlockBuffers();
                return;
            }

            SubmarineKinematicState* kinematic = (SubmarineKinematicState*)NativeArrayUnsafeUtility.GetUnsafePtr(kinematicBuffer);
            AutopilotStateDTO* states = (AutopilotStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(stateBuffer);
            AutopilotAvoidanceDTO* avoidance = (AutopilotAvoidanceDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(avoidanceBuffer);
            AutopilotRouteRangeDTO* routes = (AutopilotRouteRangeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(routeBuffer);
            byte* sdf = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(sdfBuffer);
            float3* flows = (float3*)NativeArrayUnsafeUtility.GetUnsafePtr(flowBuffer);
            AutopilotTuningDTO tuning = BuildDefaultTuning();
            if (TryResolveAutopilotVaultBuffer(_dataVault, in _tuningHandle, SubmarineAutopilotVaultRoute.AutopilotTuning, 1, out NativeArray<AutopilotTuningDTO> tuningBuffer))
                tuning = SanitizeTuning(tuningBuffer[0]);

            InitializeAutopilotBuffersJob initJob = new InitializeAutopilotBuffersJob
            {
                KinematicStates = kinematic,
                AutopilotStates = states,
                Avoidance = avoidance,
                RouteRanges = routes,
                VehicleCapacity = capacity,
                TargetSpeedFallback = tuning.TargetSpeedFallback,
                AcceptanceRadius = tuning.WaypointAcceptanceRadius
            };

            GenerateMockObstacleSDFJob sdfJob = new GenerateMockObstacleSDFJob
            {
                EncodedSdf = sdf,
                Length = SubmarineAutopilotConstants.MockSdfVoxelCount,
                Dimensions = tuning.SdfDimensions,
                Origin = tuning.SdfOrigin,
                CellSize = tuning.SdfCellSize,
                SdfRangeMeters = tuning.SdfRangeMeters
            };

            GenerateMockFlowFieldJob flowJob = new GenerateMockFlowFieldJob
            {
                FlowSamples = flows,
                Length = SubmarineAutopilotConstants.FlowSampleCount,
                Dimensions = tuning.FlowDimensions,
                Origin = tuning.FlowOrigin,
                CellSize = tuning.FlowCellSize
            };

            JobHandle initHandle = initJob.Schedule(capacity, 4);
            JobHandle sdfHandle = sdfJob.Schedule(SubmarineAutopilotConstants.MockSdfVoxelCount, 64, initHandle);
            _initHandle = flowJob.Schedule(SubmarineAutopilotConstants.FlowSampleCount, 32, sdfHandle);
            _initPending = true;
            H8Memory.RegisterActiveJob(SystemID.VehiclesPhysics, _initHandle);
        }

        private bool ScheduleSolver(float fixedDeltaTime)
        {
            if (!LockSolverBuffers())
                return false;

            int capacity = math.clamp(vehicleCapacity, 1, SubmarineAutopilotConstants.MaxVehicles);
            AutopilotTuningDTO tuning = BuildDefaultTuning();
            bool tuningResolved = TryResolveAutopilotVaultBuffer(_dataVault, in _tuningHandle, SubmarineAutopilotVaultRoute.AutopilotTuning, 1, out NativeArray<AutopilotTuningDTO> tuningBuffer);
            if (tuningResolved)
                tuning = SanitizeTuning(tuningBuffer[0]);
            tuning.ResolvedQualityWeight = ResolveRuntimeQualityWeight(tuning.GlobalQualityWeight);
            tuning.ActiveVehicleCount = capacity;
            if (tuningResolved)
                tuningBuffer[0] = tuning;

            if (!tuningResolved ||
                !TryResolveAutopilotVaultBuffer(_dataVault, in _kinematicHandle, BufferID.SubmarineKinematicStates, capacity, out NativeArray<SubmarineKinematicState> kinematicBuffer) ||
                !TryResolveAutopilotVaultBuffer(_dataVault, in _autopilotHandle, SubmarineAutopilotVaultRoute.AutopilotStates, capacity, out NativeArray<AutopilotStateDTO> stateBuffer) ||
                !TryResolveAutopilotVaultBuffer(_dataVault, in _avoidanceHandle, SubmarineAutopilotVaultRoute.AutopilotAvoidance, capacity, out NativeArray<AutopilotAvoidanceDTO> avoidanceBuffer) ||
                !TryResolveAutopilotVaultBuffer(_dataVault, in _feelerHandle, SubmarineAutopilotVaultRoute.AutopilotFeelerResults, capacity * SubmarineAutopilotConstants.MaxFeelersPerVehicle, out NativeArray<AutopilotFeelerResultDTO> feelerBuffer) ||
                !TryResolveAutopilotVaultBuffer(_dataVault, in _waypointHandle, SubmarineAutopilotVaultRoute.AutopilotWaypoints, SubmarineAutopilotConstants.WaypointCapacity, out NativeArray<AutopilotWaypointDTO> waypointBuffer) ||
                !TryResolveAutopilotVaultBuffer(_dataVault, in _routeHandle, SubmarineAutopilotVaultRoute.AutopilotRouteRanges, capacity, out NativeArray<AutopilotRouteRangeDTO> routeBuffer) ||
                !TryResolveAutopilotVaultBuffer(_dataVault, in _mockSdfHandle, SubmarineAutopilotVaultRoute.AutopilotMockSdf, SubmarineAutopilotConstants.MockSdfVoxelCount, out NativeArray<byte> sdfBuffer) ||
                !TryResolveAutopilotVaultBuffer(_dataVault, in _flowHandle, SubmarineAutopilotVaultRoute.AutopilotFlowSamples, SubmarineAutopilotConstants.FlowSampleCount, out NativeArray<float3> flowBuffer) ||
                !TryResolveAutopilotVaultBuffer(_dataVault, in _handlingProfileHandle, SubmarineAutopilotVaultRoute.AutopilotHandlingProfiles, SubmarineAutopilotConstants.HandlingProfileCapacity, out NativeArray<AutopilotHandlingProfileDTO> profileBuffer) ||
                !TryResolveAutopilotVaultBuffer(_dataVault, in _telemetryHandle, SubmarineAutopilotVaultRoute.AutopilotTelemetryRing, SubmarineAutopilotConstants.BlackBoxFrames, out NativeArray<AutopilotTelemetryEntry> telemetryBuffer) ||
                !TryResolveAutopilotVaultBuffer(_dataVault, in _telemetryCursorHandle, SubmarineAutopilotVaultRoute.AutopilotTelemetryCursor, 1, out NativeArray<uint> cursorBuffer))
            {
                UnlockBuffers();
                return false;
            }

            SubmarineKinematicState* kinematic = (SubmarineKinematicState*)NativeArrayUnsafeUtility.GetUnsafePtr(kinematicBuffer);
            AutopilotStateDTO* states = (AutopilotStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(stateBuffer);
            AutopilotAvoidanceDTO* avoidance = (AutopilotAvoidanceDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(avoidanceBuffer);
            AutopilotFeelerResultDTO* feelers = (AutopilotFeelerResultDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(feelerBuffer);
            AutopilotWaypointDTO* waypoints = (AutopilotWaypointDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(waypointBuffer);
            AutopilotRouteRangeDTO* routes = (AutopilotRouteRangeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(routeBuffer);
            byte* sdf = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(sdfBuffer);
            float3* flow = (float3*)NativeArrayUnsafeUtility.GetUnsafePtr(flowBuffer);
            AutopilotHandlingProfileDTO* profiles = (AutopilotHandlingProfileDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(profileBuffer);
            AutopilotTelemetryEntry* telemetry = (AutopilotTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetryBuffer);
            uint* cursor = (uint*)NativeArrayUnsafeUtility.GetUnsafePtr(cursorBuffer);

            EvaluateCollisionAvoidanceJob evaluate = new EvaluateCollisionAvoidanceJob
            {
                KinematicStates = kinematic,
                AutopilotStates = states,
                Avoidance = avoidance,
                FeelerResults = feelers,
                EncodedSdf = sdf,
                VehicleCount = capacity,
                FeelerResultLength = feelerBuffer.Length,
                EncodedSdfLength = sdfBuffer.Length,
                Tuning = tuning,
                GlobalQualityWeight = tuning.ResolvedQualityWeight
            };

            ComputeDesiredVelocityJob compute = new ComputeDesiredVelocityJob
            {
                KinematicStates = kinematic,
                AutopilotStates = states,
                Avoidance = avoidance,
                Waypoints = waypoints,
                RouteRanges = routes,
                FlowSamples = flow,
                HandlingProfiles = profiles,
                VehicleCount = capacity,
                WaypointLength = waypointBuffer.Length,
                FlowSampleLength = flowBuffer.Length,
                HandlingProfileLength = profileBuffer.Length,
                Tuning = tuning,
                DeltaTime = fixedDeltaTime,
                Frame = _frame
            };

            RecordAutopilotTelemetryJob telemetryJob = new RecordAutopilotTelemetryJob
            {
                KinematicStates = kinematic,
                AutopilotStates = states,
                Avoidance = avoidance,
                TelemetryRing = telemetry,
                TelemetryCursor = cursor,
                VehicleCount = capacity,
                Frame = _frame,
                GlobalQualityWeight = tuning.ResolvedQualityWeight
            };

            JobHandle evalHandle = evaluate.Schedule(capacity, 4);
            JobHandle computeHandle = compute.Schedule(capacity, 4, evalHandle);
            _solverHandle = telemetryJob.Schedule(computeHandle);
            _solverPending = true;
            _frame++;
            H8Memory.RegisterActiveJob(SystemID.VehiclesPhysics, _solverHandle);
            return true;
        }

        private bool TryFinalizePendingJobsNoWait()
        {
            if (_initPending)
            {
                if (!TryFinalizeJobHandleNoWait(ref _initHandle))
                    return false;
                _initPending = false;
                _initialized = true;
                UnlockBuffers();
            }

            if (_solverPending)
            {
                if (!TryFinalizeJobHandleNoWait(ref _solverHandle))
                    return false;
                _solverPending = false;
                CheckLatestTelemetryForFault();
                UnlockBuffers();
            }

            return true;
        }

        private bool CompletePendingJobsForTeardown()
        {
            if (_initPending)
            {
                if (!CompleteJobHandleForTeardown(ref _initHandle))
                    return false;
                _initPending = false;
                _initialized = true;
                UnlockBuffers();
            }

            if (_solverPending)
            {
                if (!CompleteJobHandleForTeardown(ref _solverHandle))
                    return false;
                _solverPending = false;
                CheckLatestTelemetryForFault();
                UnlockBuffers();
            }

            return true;
        }

        private static bool TryFinalizeJobHandleNoWait(ref JobHandle handle)
        {
            if (!handle.IsCompleted)
                return false;

            return DispatcherJobFence.TryFinalizeCompleted(ref handle);
        }

        private static bool CompleteJobHandleForTeardown(ref JobHandle handle)
        {
            return DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
        }

        private bool LockInitializationBuffers()
        {
            if (_buffersLocked || _dataVault == null)
                return false;

            _lockMask = 0u;
            if (!TryLockOwnedBuffer(BufferID.SubmarineKinematicStates, LockKinematicStates)) return false;
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotStates, LockAutopilotStates)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotAvoidance, LockAutopilotAvoidance)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotRouteRanges, LockRouteRanges)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotTuning, LockTuning)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotTelemetryCursor, LockTelemetryCursor)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotMockSdf, LockMockSdf)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotFlowSamples, LockFlowSamples)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotHandlingProfiles, LockHandlingProfiles)) { UnlockBuffers(); return false; }
            return true;
        }

        private bool LockSolverBuffers()
        {
            if (_buffersLocked || _dataVault == null)
                return false;

            _lockMask = 0u;
            if (!TryLockOwnedBuffer(BufferID.SubmarineKinematicStates, LockKinematicStates)) return false;
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotStates, LockAutopilotStates)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotAvoidance, LockAutopilotAvoidance)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotFeelerResults, LockFeelerResults)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotWaypoints, LockWaypoints)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotRouteRanges, LockRouteRanges)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotTuning, LockTuning)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotTelemetryRing, LockTelemetryRing)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotTelemetryCursor, LockTelemetryCursor)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotMockSdf, LockMockSdf)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotFlowSamples, LockFlowSamples)) { UnlockBuffers(); return false; }
            if (!TryLockOwnedBuffer(SubmarineAutopilotVaultRoute.AutopilotHandlingProfiles, LockHandlingProfiles)) { UnlockBuffers(); return false; }
            return true;
        }

        private bool TryLockOwnedBuffer(BufferID bufferId, uint lockBit)
        {
            if (_dataVault == null || !_dataVault.TryLockBuffer(bufferId, SystemID.VehiclesPhysics))
                return false;

            _lockMask |= lockBit;
            _buffersLocked = true;
            return true;
        }

        private void UnlockBuffers()
        {
            if (!_buffersLocked && _lockMask == 0u)
                return;

            uint lockMask = _lockMask;
            if (_dataVault != null)
            {
                if ((lockMask & LockKinematicStates) != 0u) _dataVault.TryUnlockBuffer(BufferID.SubmarineKinematicStates, SystemID.VehiclesPhysics);
                if ((lockMask & LockAutopilotStates) != 0u) _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotStates, SystemID.VehiclesPhysics);
                if ((lockMask & LockAutopilotAvoidance) != 0u) _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotAvoidance, SystemID.VehiclesPhysics);
                if ((lockMask & LockFeelerResults) != 0u) _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotFeelerResults, SystemID.VehiclesPhysics);
                if ((lockMask & LockWaypoints) != 0u) _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotWaypoints, SystemID.VehiclesPhysics);
                if ((lockMask & LockRouteRanges) != 0u) _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotRouteRanges, SystemID.VehiclesPhysics);
                if ((lockMask & LockTuning) != 0u) _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotTuning, SystemID.VehiclesPhysics);
                if ((lockMask & LockTelemetryRing) != 0u) _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotTelemetryRing, SystemID.VehiclesPhysics);
                if ((lockMask & LockTelemetryCursor) != 0u) _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotTelemetryCursor, SystemID.VehiclesPhysics);
                if ((lockMask & LockMockSdf) != 0u) _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotMockSdf, SystemID.VehiclesPhysics);
                if ((lockMask & LockFlowSamples) != 0u) _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotFlowSamples, SystemID.VehiclesPhysics);
                if ((lockMask & LockHandlingProfiles) != 0u) _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotHandlingProfiles, SystemID.VehiclesPhysics);
            }

            _lockMask = 0u;
            _buffersLocked = false;
        }

        private void CheckLatestTelemetryForFault()
        {
            if (!TryReadAutopilotVaultBuffer(_dataVault, in _telemetryHandle, SubmarineAutopilotVaultRoute.AutopilotTelemetryRing, SubmarineAutopilotConstants.BlackBoxFrames, out NativeArray<AutopilotTelemetryEntry> telemetry) ||
                !TryReadAutopilotVaultBuffer(_dataVault, in _telemetryCursorHandle, SubmarineAutopilotVaultRoute.AutopilotTelemetryCursor, 1, out NativeArray<uint> cursor))
                return;

            uint cursorValue = cursor[0];
            if (cursorValue == 0u)
                return;

            int latest = ((int)cursorValue - 1 + SubmarineAutopilotConstants.BlackBoxFrames) % SubmarineAutopilotConstants.BlackBoxFrames;
            uint flags = telemetry[latest].Flags;
            if ((flags & (SubmarineAutopilotConstants.NavFlagFatalNaN | SubmarineAutopilotConstants.NavFlagSlowBurst)) != 0u)
            {
                _faulted = true;
                DumpBlackBoxIfFaulted();
            }
        }

        private void DumpBlackBoxIfFaulted()
        {
            if (!_faulted || _dumped ||
                !TryReadAutopilotVaultBuffer(_dataVault, in _telemetryHandle, SubmarineAutopilotVaultRoute.AutopilotTelemetryRing, SubmarineAutopilotConstants.BlackBoxFrames, out NativeArray<AutopilotTelemetryEntry> telemetry))
                return;

            try
            {
                string logDir = Path.Combine(_projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(logDir);
                int bytes = UnsafeUtility.SizeOf<AutopilotTelemetryEntry>() * SubmarineAutopilotConstants.BlackBoxFrames;
                AutopilotTelemetryEntry* telemetryPtr = (AutopilotTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                bool wrote = WriteTelemetryDump(Path.Combine(logDir, AgentDumpFileName), telemetryPtr, bytes);
                wrote |= WriteTelemetryDump(Path.Combine(logDir, NavigationSurgeonDumpFileName), telemetryPtr, bytes);
                _dumped = wrote;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static bool WriteTelemetryDump(string path, AutopilotTelemetryEntry* telemetry, int bytes)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.SequentialScan))
                {
                    stream.Write(new ReadOnlySpan<byte>((byte*)telemetry, bytes));
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

#if UNITY_EDITOR
        private bool TryApplyHandlingProfilesCsv()
        {
            if (string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath))
                return false;

            long ticks;
            try
            {
                ticks = File.GetLastWriteTimeUtc(_csvPath).Ticks;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            if (ticks == _csvLastWriteTicks)
                return false;

            if (!_dataVault.TryLockBuffer(SubmarineAutopilotVaultRoute.AutopilotCsvScratch, SystemID.VehiclesPhysics))
                return false;

            bool profilesLocked = _dataVault.TryLockBuffer(SubmarineAutopilotVaultRoute.AutopilotHandlingProfiles, SystemID.VehiclesPhysics);
            try
            {
                if (!profilesLocked)
                    return false;

                if (!TryResolveAutopilotVaultBuffer(_dataVault, in _csvScratchHandle, SubmarineAutopilotVaultRoute.AutopilotCsvScratch, SubmarineAutopilotConstants.CsvScratchBytes, out NativeArray<byte> scratchBuffer) ||
                    !TryResolveAutopilotVaultBuffer(_dataVault, in _handlingProfileHandle, SubmarineAutopilotVaultRoute.AutopilotHandlingProfiles, SubmarineAutopilotConstants.HandlingProfileCapacity, out NativeArray<AutopilotHandlingProfileDTO> profileBuffer))
                    return false;

                byte* scratch = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratchBuffer);
                AutopilotHandlingProfileDTO* profiles = (AutopilotHandlingProfileDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(profileBuffer);
                int length = ReadCsvBytes(_csvPath, scratch, scratchBuffer.Length);
                if (length <= 0)
                    return false;

                ParseHandlingProfiles(new ReadOnlySpan<byte>(scratch, length), profiles, profileBuffer.Length);
                _csvLastWriteTicks = ticks;
                return true;
            }
            finally
            {
                if (profilesLocked)
                    _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotHandlingProfiles, SystemID.VehiclesPhysics);
                _dataVault.TryUnlockBuffer(SubmarineAutopilotVaultRoute.AutopilotCsvScratch, SystemID.VehiclesPhysics);
            }
        }
#endif

#if UNITY_EDITOR
        private static int ReadCsvBytes(string path, byte* destination, int maxBytes)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 256, FileOptions.SequentialScan))
                {
                    long capped = math.min((long)maxBytes, math.min(MaxCsvBytes, stream.Length));
                    if (capped <= 0L)
                        return 0;

                    Span<byte> target = new Span<byte>(destination, (int)capped);
                    int read = 0;
                    while (read < target.Length)
                    {
                        int chunk = stream.Read(target.Slice(read));
                        if (chunk <= 0)
                            break;
                        read += chunk;
                    }
                    return read;
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

        private static void ParseHandlingProfiles(ReadOnlySpan<byte> bytes, AutopilotHandlingProfileDTO* profiles, int profileCapacity)
        {
            for (int i = 0; i < profileCapacity; i++)
                profiles[i] = default;

            int cursor = 0;
            int length = bytes.Length;
            while (cursor < length)
            {
                int lineStart = cursor;
                while (cursor < length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                    cursor++;
                int lineEnd = cursor;
                while (cursor < length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                    cursor++;

                if (lineEnd <= lineStart || bytes[lineStart] == (byte)'#')
                    continue;

                if (TryParseProfileLine(bytes, lineStart, lineEnd, out AutopilotHandlingProfileDTO dto))
                    InsertProfile(profiles, profileCapacity, dto);
            }

            if (!ContainsProfile(profiles, profileCapacity, SubmarineAutopilotConstants.HandlingProfileDefaultHash))
                InsertProfile(profiles, profileCapacity, BuildHandlingProfile(SubmarineAutopilotConstants.HandlingProfileDefaultHash, 0.42f, 12f, 1f, 1f));
        }
#endif

        private static void WriteDefaultHandlingProfiles(AutopilotHandlingProfileDTO* profiles, int profileCapacity)
        {
            for (int i = 0; i < profileCapacity; i++)
                profiles[i] = default;

            InsertProfile(profiles, profileCapacity, BuildHandlingProfile(SubmarineAutopilotConstants.HandlingProfileDefaultHash, 0.42f, 12f, 1f, 1f));
            InsertProfile(profiles, profileCapacity, BuildHandlingProfile(SubmarineAutopilotConstants.HandlingProfileScoutHash, 0.65f, 18f, 1.2f, 0.9f));
            InsertProfile(profiles, profileCapacity, BuildHandlingProfile(SubmarineAutopilotConstants.HandlingProfileFreighterHash, 0.28f, 7f, 0.75f, 1.35f));
        }

        private static bool ContainsProfile(AutopilotHandlingProfileDTO* profiles, int profileCapacity, uint hash)
        {
            if (profiles == null || profileCapacity <= 0 || hash == 0u)
                return false;

            int start = (int)(hash % (uint)profileCapacity);
            for (int probe = 0; probe < profileCapacity; probe++)
            {
                int index = (start + probe) % profileCapacity;
                uint candidate = profiles[index].NameHash;
                if (candidate == hash)
                    return true;
                if (candidate == 0u)
                    return false;
            }

            return false;
        }

        private static AutopilotHandlingProfileDTO BuildHandlingProfile(uint hash, float turnRate, float acceleration, float speedScale, float repulsionWeight)
        {
            AutopilotHandlingProfileDTO dto = default;
            dto.NameHash = hash;
            dto.MaxTurnRateRadians = math.max(0.001f, turnRate);
            dto.AccelerationLimit = math.max(0f, acceleration);
            dto.SpeedScale = math.max(0f, speedScale);
            dto.RepulsionWeight = math.max(0f, repulsionWeight);
            dto.Flags = SubmarineAutopilotConstants.NavFlagInitialized;
            return dto;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashRouteHeader(int submarineIndex, int count)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)math.max(0, submarineIndex)) * 16777619u;
            hash = (hash ^ (uint)math.max(0, count)) * 16777619u;
            hash = (hash ^ SubmarineAutopilotConstants.SourceHashAutopilot) * 16777619u;
            return hash != 0u ? hash : SubmarineAutopilotConstants.SourceHashAutopilot;
        }

        private static void InsertProfile(AutopilotHandlingProfileDTO* profiles, int profileCapacity, AutopilotHandlingProfileDTO dto)
        {
            if (profileCapacity <= 0 || dto.NameHash == 0u)
                return;

            int start = (int)(dto.NameHash % (uint)profileCapacity);
            for (int probe = 0; probe < profileCapacity; probe++)
            {
                int index = (start + probe) % profileCapacity;
                if (profiles[index].NameHash == 0u || profiles[index].NameHash == dto.NameHash)
                {
                    profiles[index] = dto;
                    return;
                }
            }
        }

#if UNITY_EDITOR
        private static bool TryParseProfileLine(ReadOnlySpan<byte> bytes, int start, int end, out AutopilotHandlingProfileDTO dto)
        {
            dto = default;
            int cursor = start;
            uint hash = 2166136261u;
            bool hasName = false;
            while (cursor < end && bytes[cursor] != (byte)',')
            {
                byte b = bytes[cursor++];
                if (b > 32)
                {
                    hash = (hash ^ ToLowerAscii(b)) * 16777619u;
                    hasName = true;
                }
            }
            if (!hasName || cursor >= end)
                return false;

            cursor++;
            if (!TryParseFloat(bytes, ref cursor, end, out float turnRate)) return false;
            if (!TryConsumeComma(bytes, ref cursor, end)) return false;
            if (!TryParseFloat(bytes, ref cursor, end, out float acceleration)) return false;
            if (!TryConsumeComma(bytes, ref cursor, end)) return false;
            if (!TryParseFloat(bytes, ref cursor, end, out float speedScale)) return false;

            float repulsionWeight = 1f;
            if (TryConsumeComma(bytes, ref cursor, end))
            {
                if (!TryParseFloat(bytes, ref cursor, end, out repulsionWeight))
                    return false;
            }

            dto.NameHash = hash;
            dto.MaxTurnRateRadians = math.max(0.001f, turnRate);
            dto.AccelerationLimit = math.max(0f, acceleration);
            dto.SpeedScale = math.max(0f, speedScale);
            dto.RepulsionWeight = math.max(0f, repulsionWeight);
            dto.Flags = SubmarineAutopilotConstants.NavFlagInitialized;
            return true;
        }

        private static bool TryConsumeComma(ReadOnlySpan<byte> bytes, ref int cursor, int end)
        {
            while (cursor < end && bytes[cursor] <= 32)
                cursor++;
            if (cursor >= end || bytes[cursor] != (byte)',')
                return false;
            cursor++;
            return true;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, ref int cursor, int end, out float value)
        {
            value = 0f;
            while (cursor < end && bytes[cursor] <= 32)
                cursor++;

            float sign = 1f;
            if (cursor < end && bytes[cursor] == (byte)'-')
            {
                sign = -1f;
                cursor++;
            }

            float whole = 0f;
            bool any = false;
            while (cursor < end && bytes[cursor] >= (byte)'0' && bytes[cursor] <= (byte)'9')
            {
                whole = whole * 10f + (bytes[cursor] - (byte)'0');
                cursor++;
                any = true;
            }

            float fraction = 0f;
            float scale = 1f;
            if (cursor < end && bytes[cursor] == (byte)'.')
            {
                cursor++;
                while (cursor < end && bytes[cursor] >= (byte)'0' && bytes[cursor] <= (byte)'9')
                {
                    fraction = fraction * 10f + (bytes[cursor] - (byte)'0');
                    scale *= 10f;
                    cursor++;
                    any = true;
                }
            }

            if (!any)
                return false;

            value = sign * (whole + fraction / scale);
            return math.isfinite(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ToLowerAscii(byte b)
        {
            return b >= (byte)'A' && b <= (byte)'Z' ? (byte)(b + 32) : b;
        }
#endif

        private void OnDrawGizmos()
        {
            if (!drawFeelerGizmos ||
                !TryReadAutopilotVaultBuffer(
                    _dataVault,
                    in _feelerHandle,
                    SubmarineAutopilotVaultRoute.AutopilotFeelerResults,
                    math.clamp(vehicleCapacity, 1, SubmarineAutopilotConstants.MaxVehicles) * SubmarineAutopilotConstants.MaxFeelersPerVehicle,
                    out NativeArray<AutopilotFeelerResultDTO> feelers))
                return;

            int count = math.min(feelers.Length, math.clamp(vehicleCapacity, 1, SubmarineAutopilotConstants.MaxVehicles) * SubmarineAutopilotConstants.MaxFeelersPerVehicle);
            for (int i = 0; i < count; i++)
            {
                AutopilotFeelerResultDTO feeler = feelers[i];
                if ((feeler.Flags & SubmarineAutopilotConstants.FeelerFlagActive) == 0u)
                    continue;

                Gizmos.color = (feeler.Flags & SubmarineAutopilotConstants.FeelerFlagHit) != 0u ? Color.yellow : Color.green;
                Gizmos.DrawLine(ToVector3(feeler.StartRuntime), ToVector3(feeler.EndRuntime));
                if ((feeler.Flags & SubmarineAutopilotConstants.FeelerFlagHit) != 0u)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(ToVector3(feeler.HitRuntime), 0.45f);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(ToVector3(feeler.HitRuntime), ToVector3(feeler.HitRuntime + feeler.Repulsion));
                }
            }
        }

        private AutopilotTuningDTO BuildDefaultTuning()
        {
            AutopilotTuningDTO tuning = default;
            tuning.FeelerLength = math.max(1f, feelerLength);
            tuning.SdfThresholdMeters = math.max(0.1f, sdfThresholdMeters);
            tuning.RepulsionWeight = math.max(0f, repulsionWeight);
            tuning.MaxTurnRateRadians = math.max(0.001f, maxTurnRateRadians);
            tuning.WaypointAcceptanceRadius = math.max(0.1f, waypointAcceptanceRadius);
            tuning.FlowCompensationWeight = math.max(0f, flowCompensationWeight);
            tuning.TargetSpeedFallback = math.max(0f, defaultTargetSpeed);
            tuning.GlobalQualityWeight = 1f;
            tuning.SdfOrigin = new float3(-96f, -48f, -96f);
            tuning.SdfCellSize = new float3(4f, 4f, 4f);
            tuning.SdfDimensions = new int3(
                SubmarineAutopilotConstants.MockSdfWidth,
                SubmarineAutopilotConstants.MockSdfHeight,
                SubmarineAutopilotConstants.MockSdfDepth);
            tuning.SdfRangeMeters = 32f;
            tuning.ActiveVehicleCount = math.clamp(vehicleCapacity, 1, SubmarineAutopilotConstants.MaxVehicles);
            tuning.FlowOrigin = new float3(-128f, -48f, -128f);
            tuning.FlowCellSize = new float3(16f, 12f, 16f);
            tuning.FlowDimensions = new int3(
                SubmarineAutopilotConstants.FlowWidth,
                SubmarineAutopilotConstants.FlowHeight,
                SubmarineAutopilotConstants.FlowDepth);
            tuning.SourceHash = SubmarineAutopilotConstants.SourceHashAutopilot;
            tuning.ResolvedQualityWeight = ResolveRuntimeQualityWeight(tuning.GlobalQualityWeight);
            return tuning;
        }

        private static AutopilotTuningDTO SanitizeTuning(AutopilotTuningDTO tuning)
        {
            bool missingSource = tuning.SourceHash == 0u;
            if (missingSource)
                tuning.SourceHash = SubmarineAutopilotConstants.SourceHashAutopilot;
            tuning.FeelerLength = math.isfinite(tuning.FeelerLength) && tuning.FeelerLength > 0f ? tuning.FeelerLength : 72f;
            tuning.SdfThresholdMeters = math.isfinite(tuning.SdfThresholdMeters) && tuning.SdfThresholdMeters > 0f ? tuning.SdfThresholdMeters : 9f;
            tuning.RepulsionWeight = math.isfinite(tuning.RepulsionWeight) && tuning.RepulsionWeight >= 0f ? tuning.RepulsionWeight : 4.5f;
            tuning.MaxTurnRateRadians = math.isfinite(tuning.MaxTurnRateRadians) && tuning.MaxTurnRateRadians > 0f ? tuning.MaxTurnRateRadians : 0.42f;
            tuning.WaypointAcceptanceRadius = math.isfinite(tuning.WaypointAcceptanceRadius) && tuning.WaypointAcceptanceRadius > 0f ? tuning.WaypointAcceptanceRadius : 10f;
            tuning.FlowCompensationWeight = math.isfinite(tuning.FlowCompensationWeight) && tuning.FlowCompensationWeight >= 0f ? tuning.FlowCompensationWeight : 1f;
            tuning.TargetSpeedFallback = math.isfinite(tuning.TargetSpeedFallback) && tuning.TargetSpeedFallback >= 0f ? tuning.TargetSpeedFallback : 8f;
            float sourceQuality = math.select(tuning.GlobalQualityWeight, 1f, missingSource);
            tuning.GlobalQualityWeight = SanitizeQualityWeight(sourceQuality, 1f);
            tuning.ResolvedQualityWeight = SanitizeQualityWeight(tuning.ResolvedQualityWeight, tuning.GlobalQualityWeight);
            if (tuning.SdfDimensions.x <= 1 || tuning.SdfDimensions.y <= 1 || tuning.SdfDimensions.z <= 1)
                tuning.SdfDimensions = new int3(SubmarineAutopilotConstants.MockSdfWidth, SubmarineAutopilotConstants.MockSdfHeight, SubmarineAutopilotConstants.MockSdfDepth);
            if (!math.all(math.isfinite(tuning.SdfOrigin)))
                tuning.SdfOrigin = new float3(-96f, -48f, -96f);
            if (!math.all(math.isfinite(tuning.SdfCellSize)) || math.cmin(math.abs(tuning.SdfCellSize)) <= 0.001f)
                tuning.SdfCellSize = new float3(4f, 4f, 4f);
            tuning.SdfRangeMeters = math.isfinite(tuning.SdfRangeMeters) && tuning.SdfRangeMeters > 0f ? tuning.SdfRangeMeters : 32f;
            if (tuning.FlowDimensions.x <= 1 || tuning.FlowDimensions.y <= 1 || tuning.FlowDimensions.z <= 1)
                tuning.FlowDimensions = new int3(SubmarineAutopilotConstants.FlowWidth, SubmarineAutopilotConstants.FlowHeight, SubmarineAutopilotConstants.FlowDepth);
            if (!math.all(math.isfinite(tuning.FlowOrigin)))
                tuning.FlowOrigin = new float3(-128f, -48f, -128f);
            if (!math.all(math.isfinite(tuning.FlowCellSize)) || math.cmin(math.abs(tuning.FlowCellSize)) <= 0.001f)
                tuning.FlowCellSize = new float3(16f, 12f, 16f);
            return tuning;
        }

        private float ResolveSchedulingQualityWeight()
        {
            float qualityCap = 1f;
            if (_buffersReady && _dataVault != null && !_buffersLocked)
            {
                if (TryReadAutopilotVaultBuffer(_dataVault, in _tuningHandle, SubmarineAutopilotVaultRoute.AutopilotTuning, 1, out NativeArray<AutopilotTuningDTO> tuning))
                    qualityCap = SanitizeQualityWeight(tuning[0].GlobalQualityWeight, 1f);
            }

            return ResolveRuntimeQualityWeight(qualityCap);
        }

        private static float ResolveRuntimeQualityWeight(float qualityCap)
        {
            float liveQuality;
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                liveQuality = config.GlobalQualityWeight;
            else
                liveQuality = HomeostasisBrain.GlobalQualityWeight;

            float cap = SanitizeQualityWeight(qualityCap, 1f);
            float quality = math.min(SanitizeQualityWeight(liveQuality, 1f), cap);
            return QuantizeQualityWeight(quality);
        }

        private static float SanitizeQualityWeight(float value, float fallback)
        {
            return math.saturate(math.select(fallback, value, math.isfinite(value)));
        }

        private static float QuantizeQualityWeight(float value)
        {
            float quality = math.saturate(math.select(1f, value, math.isfinite(value)));
            int milli = math.clamp((int)math.floor(quality * 1000f + 0.5f), 0, 1000);
            return milli * 0.001f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveSolverCadenceFrames(float quality)
        {
            float q = math.saturate(math.isfinite(quality) ? quality : 1f);
            float curve = q * q;
            return math.clamp((int)math.round(math.lerp(12f, 1f, curve)), 1, 12);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFixedDeltaTime(float fixedDeltaTime)
        {
            float safe = math.isfinite(fixedDeltaTime) && fixedDeltaTime > 0f ? fixedDeltaTime : 1f / 60f;
            return math.clamp(safe, 0.0001f, 0.25f);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo assets = Directory.GetParent(Application.dataPath);
            return assets != null ? assets.FullName : Directory.GetCurrentDirectory();
        }

        private static string ResolveHandlingProfilesCsvPath(string projectRoot)
        {
            string projectDataPath = Path.Combine(projectRoot, "Assets", "_Project", "Data", "Vehicles", "vehicle_handling_profiles.csv");
            if (File.Exists(projectDataPath))
                return projectDataPath;

            return Path.Combine(projectRoot, "vehicle_handling_profiles.csv");
        }
    }
}
