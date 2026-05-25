using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts.AI.Cognition
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
    /// Runtime flags consumed by the Alpha Leviathan stalk solver.
    /// </summary>
    public static class AlphaLeviathanStalkRuntimeFlags
    {
        public const uint Active = 1u << 0;
        public const uint MathLodSurvival = 1u << 1;
        public const uint SdfContourRequested = 1u << 2;
        public const uint HasPlayerAnchor = 1u << 3;
        public const uint HasSonarPing = 1u << 4;
        public const uint ShiftFenceActive = 1u << 5;
    }

    /// <summary>
    /// Output-side intent bits that are not constrained by the legacy telemetry byte.
    /// </summary>
    public static class AlphaLeviathanSteeringIntentFlags
    {
        public const byte SurvivalRadialFallback = 1 << 0;
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
        public const float SurvivalSteeringBlend = 0.2f;
        public const float PrecisionSteeringBlend = 0.55f;
        public const float SurvivalCadenceSeconds = 0.2f;
        public const float PrecisionCadenceSeconds = 0.016666668f;
        public const float SdfContourWeight = 0.45f;
        public const float VisualOverkillMax01 = 1f;
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
        [FieldOffset(24)] public ulong Reserved;
        [FieldOffset(32)] public float4 Local;

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
        [FieldOffset(136)]
        public uint Reserved0;
        [FieldOffset(140)]
        public ushort Slot;
        [FieldOffset(142)]
        public byte CurrentPhase;
        [FieldOffset(143)]
        public byte Flags;
        [FieldOffset(144)]
        private byte _pad0;
        [FieldOffset(145)]
        private byte _pad1;
        [FieldOffset(146)]
        private byte _pad2;
        [FieldOffset(147)]
        private byte _pad3;
        [FieldOffset(148)]
        private byte _pad4;
        [FieldOffset(149)]
        private byte _pad5;
        [FieldOffset(150)]
        private byte _pad6;
        [FieldOffset(151)]
        private byte _pad7;
        [FieldOffset(152)]
        private byte _pad8;
        [FieldOffset(153)]
        private byte _pad9;
        [FieldOffset(154)]
        private byte _pad10;
        [FieldOffset(155)]
        private byte _pad11;
        [FieldOffset(156)]
        private byte _pad12;
        [FieldOffset(157)]
        private byte _pad13;
        [FieldOffset(158)]
        private byte _pad14;
        [FieldOffset(159)]
        private byte _pad15;
        [FieldOffset(160)]
        private byte _pad16;
        [FieldOffset(161)]
        private byte _pad17;
        [FieldOffset(162)]
        private byte _pad18;
        [FieldOffset(163)]
        private byte _pad19;
        [FieldOffset(164)]
        private byte _pad20;
        [FieldOffset(165)]
        private byte _pad21;
        [FieldOffset(166)]
        private byte _pad22;
        [FieldOffset(167)]
        private byte _pad23;
        [FieldOffset(168)]
        private byte _pad24;
        [FieldOffset(169)]
        private byte _pad25;
        [FieldOffset(170)]
        private byte _pad26;
        [FieldOffset(171)]
        private byte _pad27;
        [FieldOffset(172)]
        private byte _pad28;
        [FieldOffset(173)]
        private byte _pad29;
        [FieldOffset(174)]
        private byte _pad30;
        [FieldOffset(175)]
        private byte _pad31;
        [FieldOffset(176)]
        private byte _pad32;
        [FieldOffset(177)]
        private byte _pad33;
        [FieldOffset(178)]
        private byte _pad34;
        [FieldOffset(179)]
        private byte _pad35;
        [FieldOffset(180)]
        private byte _pad36;
        [FieldOffset(181)]
        private byte _pad37;
        [FieldOffset(182)]
        private byte _pad38;
        [FieldOffset(183)]
        private byte _pad39;
        [FieldOffset(184)]
        private byte _pad40;
        [FieldOffset(185)]
        private byte _pad41;
        [FieldOffset(186)]
        private byte _pad42;
        [FieldOffset(187)]
        private byte _pad43;
        [FieldOffset(188)]
        private byte _pad44;
        [FieldOffset(189)]
        private byte _pad45;
        [FieldOffset(190)]
        private byte _pad46;
        [FieldOffset(191)]
        private byte _pad47;
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
    /// DataVault-owned steering output row produced by the Alpha Leviathan stalk solver.
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
        private byte _pad1;
        [FieldOffset(87)]
        private byte _pad2;
        [FieldOffset(88)]
        private byte _pad3;
        [FieldOffset(89)]
        private byte _pad4;
        [FieldOffset(90)]
        private byte _pad5;
        [FieldOffset(91)]
        private byte _pad6;
        [FieldOffset(92)]
        private byte _pad7;
        [FieldOffset(93)]
        private byte _pad8;
        [FieldOffset(94)]
        private byte _pad9;
        [FieldOffset(95)]
        private byte _pad10;
        [FieldOffset(96)]
        private byte _pad11;
        [FieldOffset(97)]
        private byte _pad12;
        [FieldOffset(98)]
        private byte _pad13;
        [FieldOffset(99)]
        private byte _pad14;
        [FieldOffset(100)]
        private byte _pad15;
        [FieldOffset(101)]
        private byte _pad16;
        [FieldOffset(102)]
        private byte _pad17;
        [FieldOffset(103)]
        private byte _pad18;
        [FieldOffset(104)]
        private byte _pad19;
        [FieldOffset(105)]
        private byte _pad20;
        [FieldOffset(106)]
        private byte _pad21;
        [FieldOffset(107)]
        private byte _pad22;
        [FieldOffset(108)]
        private byte _pad23;
        [FieldOffset(109)]
        private byte _pad24;
        [FieldOffset(110)]
        private byte _pad25;
        [FieldOffset(111)]
        private byte _pad26;
        [FieldOffset(112)]
        private byte _pad27;
        [FieldOffset(113)]
        private byte _pad28;
        [FieldOffset(114)]
        private byte _pad29;
        [FieldOffset(115)]
        private byte _pad30;
        [FieldOffset(116)]
        private byte _pad31;
        [FieldOffset(117)]
        private byte _pad32;
        [FieldOffset(118)]
        private byte _pad33;
        [FieldOffset(119)]
        private byte _pad34;
        [FieldOffset(120)]
        private byte _pad35;
        [FieldOffset(121)]
        private byte _pad36;
        [FieldOffset(122)]
        private byte _pad37;
        [FieldOffset(123)]
        private byte _pad38;
        [FieldOffset(124)]
        private byte _pad39;
        [FieldOffset(125)]
        private byte _pad40;
        [FieldOffset(126)]
        private byte _pad41;
        [FieldOffset(127)]
        private byte _pad42;
    }
}
