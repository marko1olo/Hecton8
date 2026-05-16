using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.AI.Cognition
{
    /// <summary>
    /// Stalking state machine phases for the Alpha Leviathan tangent-orbit solver.
    /// </summary>
    public static class AlphaLeviathanStalkPhase
    {
        public const byte Idle = 0;
        public const byte Circle = 1;
        public const byte Charge = 2;
        public const byte Retreat = 3;
    }

    /// <summary>
    /// Runtime flags consumed by <see cref="LeviathanStalkJob"/>.
    /// </summary>
    public static class AlphaLeviathanStalkRuntimeFlags
    {
        public const uint Active = 1u << 0;
        public const uint MathLodLow = 1u << 1;
        public const uint HighTierSdfContour = 1u << 2;
        public const uint HasPlayerAnchor = 1u << 3;
        public const uint HasSonarPing = 1u << 4;
        public const uint ShiftFenceActive = 1u << 5;
    }

    /// <summary>
    /// Numeric constants for tangent-orbit predator cognition.
    /// </summary>
    public static class AlphaLeviathanStalkConstants
    {
        public const int MaxLeviathanSlots = 64;
        public const int TelemetryFrames = 300;
        public const int TelemetryCapacity = TelemetryFrames * MaxLeviathanSlots;
        public const double AupCellSizeMeters = 5000d;
        public const float MinimumFogRingMeters = 8f;
        public const float FogEdgeOffsetMeters = 5f;
        public const float DirectionEpsilon = 0.0001f;
        public const double DoubleDirectionEpsilon = 0.0001d;
        public const float NoiseAggressionGainPerSecond = 0.1f;
        public const float ChargeAggressionThreshold = 0.82f;
        public const float LightRetreatDot = 0.9f;
        public const float SonarLureHoldSeconds = 10f;
        public const float LowTierSteeringBlend = 0.2f;
        public const float HighTierSteeringBlend = 0.55f;
        public const float LowTierCadenceSeconds = 0.2f;
        public const float HighTierCadenceSeconds = 0.016666668f;
        public const float HighTierSdfContourWeight = 0.45f;
        public const float HighTierVisualOverkill01 = 1f;
    }

    /// <summary>
    /// Blittable AUP payload local to AI/Cognition so the solver does not depend on World runtime classes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 48)]
    public struct AlphaLeviathanAup
    {
        [FieldOffset(0)] public long GridX;
        [FieldOffset(8)] public long GridY;
        [FieldOffset(16)] public long GridZ;
        [FieldOffset(24)] public float4 Local;
        [FieldOffset(40)] public ulong Reserved;

        /// <summary>
        /// Converts the payload into absolute meter space for Burst-safe double precision distance math.
        /// </summary>
        /// <returns>Absolute universe position in meters.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly double3 ToAbsoluteDouble3()
        {
            return new double3(
                (GridX * AlphaLeviathanStalkConstants.AupCellSizeMeters) + Local.x,
                (GridY * AlphaLeviathanStalkConstants.AupCellSizeMeters) + Local.y,
                (GridZ * AlphaLeviathanStalkConstants.AupCellSizeMeters) + Local.z);
        }
    }

    /// <summary>
    /// DataVault-owned truth state for one Alpha Leviathan slot.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 144)]
    public struct AlphaLeviathanCognitionState
    {
        public AlphaLeviathanAup LeviathanAup;
        public AlphaLeviathanAup TargetAnchorAup;
        public float3 Forward;
        public float3 PreviousSteeringDirection;
        public float AgressionLevel01;
        public float PhaseStartSeconds;
        public uint LastShiftFrameId;
        public uint StateHash;
        public ushort Slot;
        public byte CurrentPhase;
        public byte Flags;
        public uint Reserved0;
    }

    /// <summary>
    /// DataVault-owned sensory row consumed by tangent-orbit steering.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 176)]
    public struct AlphaLeviathanSensoryStimulus
    {
        public AlphaLeviathanAup PlayerAup;
        public AlphaLeviathanAup PingAup;
        public float3 PlayerForward;
        public float3 SdfGradient;
        public float PlayerNoise01;
        public float NoiseThreshold01;
        public float HeadlightDot;
        public float FogDistanceMeters;
        public float DeltaTime;
        public float SystemStress01;
        public float SonarPingAgeSeconds;
        public float SonarPingIntensity01;
        public float CurrentTimeSeconds;
        public uint RuntimeFlags;
        public uint ObservedShiftFrameId;
        public uint Reserved0;
        public uint Reserved1;
    }

    /// <summary>
    /// DataVault-owned steering output row produced by <see cref="LeviathanStalkJob"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    public struct AlphaLeviathanSteeringOutput
    {
        public float3 DesiredDirection;
        public float3 TargetRuntimeOffsetMeters;
        public float DesiredRingDistanceMeters;
        public float DistanceToAnchorMeters;
        public float BioluminescenceIntensity;
        public float AgressionLevel01;
        public uint StateHash;
        public float SdfContourWeight01;
        public float WakeSiltIntensity01;
        public float VisualOverkill01;
        public float RecommendedCadenceSeconds;
        public ushort Slot;
        public byte CurrentPhase;
        public byte Flags;
    }
}
