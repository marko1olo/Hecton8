using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Mathematics;

namespace Hecton8.AI.Cognition
{
    /// <summary>
    /// Stalking state machine phases for the Alpha Leviathan tangent-orbit solver.
    /// </summary>
    public static class AlphaLeviathanStalkPhase
    {
        public const byte Idle = AlphaLeviathanPhase.Hidden;
        public const byte Circle = AlphaLeviathanPhase.Circling;
        public const byte Charge = AlphaLeviathanPhase.FalseCharge;
        public const byte Retreat = AlphaLeviathanPhase.VeerOff;
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
    /// Output-side intent bits that are not constrained by the legacy telemetry byte.
    /// </summary>
    public static class AlphaLeviathanSteeringIntentFlags
    {
        public const byte LowTierRadialFallback = 1 << 0;
        public const byte SdfContourRequested = 1 << 1;
        public const byte PlayerGazeBreak = 1 << 2;
        public const byte AcousticLure = 1 << 3;
        public const byte LightRetreat = 1 << 4;
        public const byte ShiftFenceActive = 1 << 5;
        public const byte FaultedInput = 1 << 6;
    }

    /// <summary>
    /// Numeric constants for tangent-orbit predator cognition.
    /// </summary>
    public static class AlphaLeviathanStalkConstants
    {
        public const int MaxLeviathanSlots = 64;
        public const int TelemetryFrames = 300;
        public const int TelemetryCapacity = TelemetryFrames * MaxLeviathanSlots;
        public const double AupCellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersDouble;
        public const float MinimumFogRingMeters = 8f;
        public const float FogEdgeOffsetMeters = 5f;
        public const float MaxFogDistanceMeters = 2048f;
        public const float DirectionEpsilon = 0.0001f;
        public const double DoubleDirectionEpsilon = 0.0001d;
        public const float NoiseAggressionGainPerSecond = 0.1f;
        public const float ChargeAggressionThreshold = 0.82f;
        public const float LightRetreatDot = 0.9f;
        public const float PlayerGazeBreakDot = 0.75f;
        public const float SonarLureHoldSeconds = 10f;
        public const float MaxDeltaTimeSeconds = 0.25f;
        public const float LowTierSteeringBlend = 0.2f;
        public const float HighTierSteeringBlend = 0.55f;
        public const float LowTierCadenceSeconds = 0.2f;
        public const float HighTierCadenceSeconds = 0.016666668f;
        public const float HighTierSdfContourWeight = 0.45f;
        public const float HighTierVisualOverkill01 = 1f;
        public const float TriangleNoiseInvPeriod = 0.0009765625f;
    }

    /// <summary>
    /// Blittable AUP payload local to AI/Cognition so the solver does not depend on World runtime classes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
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
            float4 local = math.select(float4.zero, Local, math.isfinite(Local));
            return new double3(
                (GridX * AlphaLeviathanStalkConstants.AupCellSizeMeters) + local.x,
                (GridY * AlphaLeviathanStalkConstants.AupCellSizeMeters) + local.y,
                (GridZ * AlphaLeviathanStalkConstants.AupCellSizeMeters) + local.z);
        }
    }

    /// <summary>
    /// DataVault-owned truth state for one Alpha Leviathan slot.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    public struct AlphaLeviathanCognitionState
    {
        [FieldOffset(0)]
        public AlphaLeviathanAup LeviathanAup;
        [FieldOffset(48)]
        public AlphaLeviathanAup TargetAnchorAup;
        [FieldOffset(96)]
        public float3 Forward;
        [FieldOffset(108)]
        public float3 PreviousSteeringDirection;
        [FieldOffset(120)]
        public float AgressionLevel01;
        [FieldOffset(124)]
        public float PhaseStartSeconds;
        [FieldOffset(128)]
        public uint LastShiftFrameId;
        [FieldOffset(132)]
        public uint StateHash;
        [FieldOffset(140)]
        public ushort Slot;
        [FieldOffset(142)]
        public byte CurrentPhase;
        [FieldOffset(143)]
        public byte Flags;
        [FieldOffset(136)]
        public uint Reserved0;
        [FieldOffset(144)]
        private ulong _pad1;
        [FieldOffset(152)]
        private ulong _pad2;
        [FieldOffset(160)]
        private ulong _pad3;
        [FieldOffset(168)]
        private ulong _pad4;
        [FieldOffset(176)]
        private ulong _pad5;
        [FieldOffset(184)]
        private ulong _pad6;
    }

    /// <summary>
    /// DataVault-owned sensory row consumed by tangent-orbit steering.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 176)]
    public struct AlphaLeviathanSensoryStimulus
    {
        [FieldOffset(0)]
        public AlphaLeviathanAup PlayerAup;
        [FieldOffset(48)]
        public AlphaLeviathanAup PingAup;
        [FieldOffset(96)]
        public float3 PlayerForward;
        [FieldOffset(108)]
        public float3 SdfGradient;
        [FieldOffset(120)]
        public float PlayerNoise01;
        [FieldOffset(124)]
        public float NoiseThreshold01;
        [FieldOffset(128)]
        public float HeadlightDot;
        [FieldOffset(132)]
        public float FogDistanceMeters;
        [FieldOffset(136)]
        public float DeltaTime;
        [FieldOffset(140)]
        public float SystemStress01;
        [FieldOffset(144)]
        public float SonarPingAgeSeconds;
        [FieldOffset(148)]
        public float SonarPingIntensity01;
        [FieldOffset(152)]
        public float CurrentTimeSeconds;
        [FieldOffset(156)]
        public uint RuntimeFlags;
        [FieldOffset(160)]
        public uint ObservedShiftFrameId;
        [FieldOffset(164)]
        public uint Reserved0;
        [FieldOffset(168)]
        public uint Reserved1;
        [FieldOffset(172)]
        private uint _pad0;
    }

    /// <summary>
    /// DataVault-owned steering output row produced by <see cref="LeviathanStalkJob"/>.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct AlphaLeviathanSteeringOutput
    {
        [FieldOffset(0)]
        public float3 DesiredDirection;
        [FieldOffset(12)]
        public float3 TargetRuntimeOffsetMeters;
        [FieldOffset(24)]
        public float DesiredRingDistanceMeters;
        [FieldOffset(28)]
        public float DistanceToAnchorMeters;
        [FieldOffset(32)]
        public float BioluminescenceIntensity;
        [FieldOffset(36)]
        public float AgressionLevel01;
        [FieldOffset(40)]
        public uint StateHash;
        [FieldOffset(44)]
        public float SdfContourWeight01;
        [FieldOffset(48)]
        public float WakeSiltIntensity01;
        [FieldOffset(52)]
        public float VisualOverkill01;
        [FieldOffset(56)]
        public float RecommendedCadenceSeconds;
        [FieldOffset(60)]
        public float VisorSaltCrystalGrowth01;
        [FieldOffset(64)]
        public float HullDentImpulse01;
        [FieldOffset(68)]
        public float SubsurfaceScatterPulse01;
        [FieldOffset(72)]
        public float ParticleOverkillBudget01;
        [FieldOffset(76)]
        public float PredatorSilhouetteNoise01;
        [FieldOffset(80)]
        public ushort Slot;
        [FieldOffset(82)]
        public byte CurrentPhase;
        [FieldOffset(83)]
        public byte Flags;
        [FieldOffset(84)]
        public byte IntentFlags;
        [FieldOffset(85)]
        private byte _pad0;
        [FieldOffset(86)]
        private ushort _pad1;
        [FieldOffset(88)]
        private ulong _pad2;
        [FieldOffset(96)]
        private ulong _pad3;
        [FieldOffset(104)]
        private ulong _pad4;
        [FieldOffset(112)]
        private ulong _pad5;
        [FieldOffset(120)]
        private ulong _pad6;
    }
}
