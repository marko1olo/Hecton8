using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>Authoritative thermodynamics temperature change signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TemperatureChangedSignal : ISignal
    {
        public const byte FlagPlayerAmbient = 1 << 0;
        public const byte FlagSubmarineAmbient = 1 << 1;
        public const byte FlagThermalShock = 1 << 2;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float TemperatureCelsius;
        [FieldOffset(52)] public float DeltaCelsius;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public ushort SourceId;
        [FieldOffset(62)] public byte Flags;
        [FieldOffset(63)] private byte _padTail0;
    }

    /// <summary>Producer-agnostic heat source registration/update for the abyssal thermodynamics field. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ThermalSourceSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float IntensityCelsiusPerSecond;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public uint Frame;
    }

    /// <summary>Radiation source registration/update signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RadiationSourceSignal : ISignal
    {
        public const byte OperationRemove = 0;
        public const byte OperationUpsert = 1;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float Intensity;
        [FieldOffset(52)] public float RadiusMeters;
        [FieldOffset(56)] public int SourceId;
        [FieldOffset(60)] public byte Operation;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] private ushort _padTail0;
    }

    /// <summary>Resource depletion persistence delta signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ResourceDepletionDeltaSignal : ISignal
    {
        [FieldOffset(0)] public long SectorHash;
        [FieldOffset(8)] public ulong DepletionMask;
        [FieldOffset(16)] public uint OreHash;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public ushort WordIndex;
        [FieldOffset(26)] public byte Operation;
        [FieldOffset(27)] public byte Flags;
        [FieldOffset(28)] private uint _padTail0;
    }

    /// <summary>AUP sector pre-shift warning signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AupPreShiftSignal : ISignal
    {
        [FieldOffset(0)] public float3 ShiftMeters;
        [FieldOffset(12)] public uint ShiftFrameId;
        [FieldOffset(16)] public int3 SectorDelta;
        [FieldOffset(28)] public uint Flags;
    }

    /// <summary>AUP sector shift broadcast signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AupShiftSignal : ISignal
    {
        [FieldOffset(0)] public float3 ShiftMeters;
        [FieldOffset(12)] public uint ShiftFrameId;
        [FieldOffset(16)] public int3 SectorDelta;
        [FieldOffset(28)] public uint Flags;
    }

    /// <summary>Drop-pod landing anchor for first-hour economy weighting. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DropPodLandedSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint SourceHash;
        [FieldOffset(56)] public byte Flags;
        [FieldOffset(57)] private byte _padTail0;
        [FieldOffset(58)] private ushort _padTail1;
        [FieldOffset(60)] private uint _padTail2;
    }

    /// <summary>Producer-agnostic procedural flora wake signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WakeGeneratedSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float3 Velocity;
        [FieldOffset(60)] public uint SourceFlags;
    }

    /// <summary>Producer-agnostic visual-fluid impulse. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct FluidImpulseSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float3 Vector;
        [FieldOffset(60)] public float Radius;
        [FieldOffset(64)] public float Lifetime;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public uint SourceHash;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] public ulong Reserved0;
        [FieldOffset(88)] public ulong Reserved1;
        [FieldOffset(96)] public ulong Reserved2;
        [FieldOffset(104)] public ulong Reserved3;
        [FieldOffset(112)] public ulong Reserved4;
        [FieldOffset(120)] public ulong Reserved5;
    }

    /// <summary>Bounded submarine bubble-spawn marker for visual-fluid VFX. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct BubbleSpawnSignal : ISignal
    {
        public const int ExpectedCapacity = 64;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 512036682u; // FNV32("BubbleSpawnSignal")
        public const uint FlagEngineVent = 1u << 0;
        public const uint FlagTailHeavy = 1u << 1;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float3 Direction;
        [FieldOffset(60)] public float Intensity01;
        [FieldOffset(64)] public float RadiusMeters;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public uint SourceHash;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] public ulong Reserved0;
        [FieldOffset(88)] public ulong Reserved1;
        [FieldOffset(96)] public ulong Reserved2;
        [FieldOffset(104)] public ulong Reserved3;
        [FieldOffset(112)] public ulong Reserved4;
        [FieldOffset(120)] public ulong Reserved5;
    }

    /// <summary>Narrative POI-to-progression broadcast. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ProgressionEventSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint PoiHash;
        [FieldOffset(52)] public uint QuestHash;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public byte Source;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] private ushort _padTail0;
    }

    /// <summary>First-party meta progression notification without managed strings. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ProgressionMetaSignal : ISignal
    {
        public const byte KindAchievementUnlocked = 1;
        public const byte KindAdvisoryIssued = 2;
        public const byte KindBiomeDiscovered = 3;

        [FieldOffset(0)] public uint EventHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public byte Kind;
        [FieldOffset(13)] public byte Flags;
        [FieldOffset(14)] private ushort _pad0;
        [FieldOffset(16)] public uint ContextHash;
        [FieldOffset(20)] private uint _pad1;
        [FieldOffset(24)] private ulong _pad2;
    }

    /// <summary>First-party session lifecycle notification without managed strings. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SessionLifecycleSignal : ISignal
    {
        public const byte KindGameLoaded = 1;
        public const byte KindPlayerSpawned = 2;

        [FieldOffset(0)] public ulong PlayerEntityId;
        [FieldOffset(8)] public float3 PlayerPosition;
        [FieldOffset(20)] public uint SlotHash;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint Sequence;
        [FieldOffset(32)] public byte Kind;
        [FieldOffset(33)] public byte Flags;
        [FieldOffset(34)] private ushort _pad0;
        [FieldOffset(36)] private uint _pad1;
        [FieldOffset(40)] private ulong _pad2;
        [FieldOffset(48)] private ulong _pad3;
        [FieldOffset(56)] private ulong _pad4;
    }

    /// <summary>AUP-independent global narrative state mutation. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct GlobalWorldStateSignal : ISignal
    {
        public const byte ChangeKindRule = 1;
        public const byte ChangeKindLoad = 2;
        public const byte ChangeKindDevConsole = 3;
        public const byte FlagAupIndependent = 1 << 0;
        public const byte FlagVisualRefresh = 1 << 1;
        public const byte FlagAudioBroadcast = 1 << 2;
        public const byte FlagCartographyRefresh = 1 << 3;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint VariableHash;
        [FieldOffset(52)] public int Value;
        [FieldOffset(56)] public uint StageHash;
        [FieldOffset(60)] public byte ChangeKind;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] public ushort Sequence;
    }

    /// <summary>Narrative-driven biome transition broadcast. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BiomeChangedSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint PreviousBiomeHash;
        [FieldOffset(52)] public uint CurrentBiomeHash;
        [FieldOffset(56)] public uint PoiHash;
        [FieldOffset(60)] public uint Frame;
    }

    /// <summary>Mathematical SDF biome boundary blend broadcast. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct BiomeGradientSignal : ISignal
    {
        public const byte FlagLowTierKernel = 1 << 0;
        public const byte FlagExactCellCenter = 1 << 1;
        public const byte FlagMissingMap = 1 << 2;
        public const byte FlagInvalidInput = 1 << 3;
        public const byte FlagHasSecondaryBiome = 1 << 4;
        public const byte FlagOutOfBounds = 1 << 5;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint BiomeAHash;
        [FieldOffset(52)] public uint BiomeBHash;
        [FieldOffset(56)] public float BlendFactor01;
        [FieldOffset(60)] public float BoundaryDistanceMeters;
        [FieldOffset(64)] public float CellSizeMeters;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public byte BiomeA;
        [FieldOffset(73)] public byte BiomeB;
        [FieldOffset(74)] public byte SampleDiameter;
        [FieldOffset(75)] public byte Flags;
        [FieldOffset(76)] public uint Reserved0;
        [FieldOffset(80)] public ulong Reserved1;
        [FieldOffset(88)] public ulong Reserved2;
        [FieldOffset(96)] public ulong Reserved3;
        [FieldOffset(104)] public ulong Reserved4;
        [FieldOffset(112)] public ulong Reserved5;
        [FieldOffset(120)] public ulong Reserved6;
    }

    /// <summary>Soft narrative camera focus target. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct NarrativeFocusSignal : ISignal
    {
        public const byte FlagArtifactTarget = 1 << 0;
        public const byte FlagCreatureTarget = 1 << 1;
        public const byte FlagHeadBoneTarget = 1 << 2;
        public const byte FlagWorldSubtitle = 1 << 3;
        public const byte FlagDisableFovNarrowing = 1 << 4;

        [FieldOffset(0)] public AbsoluteUniversePosition TargetAup;
        [FieldOffset(48)] public uint FocusHash;
        [FieldOffset(52)] public uint SubtitleHash;
        [FieldOffset(56)] public float Intensity01;
        [FieldOffset(60)] public float DurationSeconds;
        [FieldOffset(64)] public float SubtitleFadeDistanceSq;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public byte Flags;
        [FieldOffset(73)] public byte BoneTarget;
        [FieldOffset(74)] public ushort Reserved0;
        [FieldOffset(76)] public uint Reserved1;
        [FieldOffset(80)] public ulong Reserved2;
        [FieldOffset(88)] public ulong Reserved3;
        [FieldOffset(96)] public ulong Reserved4;
        [FieldOffset(104)] public ulong Reserved5;
        [FieldOffset(112)] public ulong Reserved6;
        [FieldOffset(120)] public ulong Reserved7;
    }

    /// <summary>Player override notification for broken narrative camera focus. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FocusBrokenSignal : ISignal
    {
        public const byte ReasonPlayerLookInput = 1;

        [FieldOffset(0)] public uint FocusHash;
        [FieldOffset(4)] public float PlayerInputDeltaSq;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public byte Reason;
        [FieldOffset(13)] public byte Flags;
        [FieldOffset(14)] private ushort _padTail0;
        [FieldOffset(16)] private ulong _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>Signal-only mixer state request. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MixerStateSignal : ISignal
    {
        public const uint FocusStateHash = 0x464F4355u; // FOCU

        [FieldOffset(0)] public uint MixerStateHash;
        [FieldOffset(4)] public uint SourceHash;
        [FieldOffset(8)] public float Intensity01;
        [FieldOffset(12)] public float DuckingDb;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte Flags;
        [FieldOffset(21)] private byte _padTail0;
        [FieldOffset(22)] private ushort _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>Diegetic HUD waypoint payload sourced from an active narrative POI. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct NarrativeHudWaypointSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint PoiHash;
        [FieldOffset(52)] public uint QuestHash;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public byte Priority;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] private ushort _padTail0;
    }

    /// <summary>Audio ambience profile payload sourced from a narrative POI. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SoundscapeProfileSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint ProfileHash;
        [FieldOffset(52)] public uint PoiHash;
        [FieldOffset(56)] public float Intensity01;
        [FieldOffset(60)] public uint Frame;
    }

    /// <summary>Save/RLE sync payload for narrative POI trigger latches. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct NarrativePoiStateSignal : ISignal
    {
        [FieldOffset(0)] public ulong StateMask;
        [FieldOffset(8)] public uint PoiHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public ushort PoiIndex;
        [FieldOffset(18)] public byte Operation;
        [FieldOffset(19)] public byte Flags;
        [FieldOffset(20)] private uint _padTail0;
        [FieldOffset(24)] private ulong _padTail1;
    }

    /// <summary>Logistics-to-UI brownout signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BrownoutSignal : ISignal
    {
        [FieldOffset(0)] public uint NetworkId;
        [FieldOffset(4)] public uint NodeId;
        [FieldOffset(8)] public float SupplyRatio;
        [FieldOffset(12)] public float Severity01;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte Priority;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(22)] private ushort _padTail0;
        [FieldOffset(24)] private ulong _padTail1;
    }

    /// <summary>Ecosystem-to-VFX debris spawn signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DebrisSpawnSignal : ISignal
    {
        public const byte DebrisKindSparks = 1;
        public const byte DebrisKindOrganicScrap = 2;
        public const byte DebrisKindWaterSplash = 3;
        public const byte DebrisKindRockShard = 10;
        public const byte FlagToolSparks = 1 << 0;
        public const byte FlagComputeShard = 1 << 7;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint SpeciesHash;
        [FieldOffset(52)] public uint SourceEntityId;
        [FieldOffset(56)] public float Intensity01;
        [FieldOffset(60)] public byte DebrisKind;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] public ushort Quantity;
    }

    /// <summary>Combat-to-feedback armor deflection signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DeflectSignal : ISignal
    {
        public const int ExpectedCapacity = 128;
        public const int MaxFrameSignals = 128;
        public const int LowTierFrameSignals = 32;
        public const uint LaneHash = 2742711508u; // FNV32("DeflectSignal")

        [FieldOffset(0)]
        public float3 LocalPoint;
        [FieldOffset(12)]
        public float FrontDot;
        [FieldOffset(16)]
        public uint TargetHash;
        [FieldOffset(20)]
        public uint SourceHash;
        [FieldOffset(24)]
        public float DamageScalar;
        [FieldOffset(28)]
        public byte Flags;
        [FieldOffset(29)]
        public byte ArmorClass;
        [FieldOffset(30)]
        public ushort Reserved;
    }

    /// <summary>Combat-to-ecosystem death signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct EntityDeathSignal : ISignal
    {
        /// <summary>Fauna-owned death: SourceHash carries the stable species hash for carrion decay profiles.</summary>
        public const byte FlagFaunaBrainCarrion = 1 << 1;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint EntityHash;
        [FieldOffset(52)] public uint SourceHash;
        [FieldOffset(56)] public float Intensity01;
        [FieldOffset(60)] public byte Flags;
        [FieldOffset(61)] private byte _padTail0;
        [FieldOffset(62)] private ushort _padTail1;
    }

    /// <summary>Data-only entity activation signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct EntitySpawnSignal : ISignal
    {
        public const byte KindEcology = 1;
        public const byte FlagEcology = 1 << 0;
        public const byte FlagSurvivalPressureVisual = 1 << 1;
        public const byte FlagSdfEmergence = 1 << 2;
        public const byte FlagVisualOverkillCompatibility = 1 << 3;
        public const byte FlagLowTierVisual = FlagSurvivalPressureVisual;
        public const byte FlagHighTierOverkill = FlagVisualOverkillCompatibility;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint SourceHash;
        [FieldOffset(52)] public ushort SpawnedCount;
        [FieldOffset(54)] public ushort RequestedCount;
        [FieldOffset(56)] public byte EntityKind;
        [FieldOffset(57)] public byte QualityWeightQ8;
        [FieldOffset(57)] public byte QualityTier;
        [FieldOffset(58)] public byte Flags;
        [FieldOffset(59)] public byte SurvivalPressureQ8;
        [FieldOffset(59)] public byte Reserved;
        [FieldOffset(60)] public uint Frame;
    }

    /// <summary>Narrative-to-celestial solar flare signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SolarFlareSignal : ISignal
    {
        [FieldOffset(0)] public uint QuestStepHash;
        [FieldOffset(4)] public float Intensity01;
        [FieldOffset(8)] public float DurationSeconds;
        [FieldOffset(12)] public uint Seed;
        [FieldOffset(16)] public byte Flags;
        [FieldOffset(17)] private byte _padTail0;
        [FieldOffset(18)] private ushort _padTail1;
        [FieldOffset(20)] private uint _padTail2;
        [FieldOffset(24)] private ulong _padTail3;
    }

    /// <summary>Origin rebase broadcast signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RebaseSignal : ISignal
    {
        [FieldOffset(0)] public float3 ShiftMeters;
        [FieldOffset(12)] public uint ShiftFrameId;
        [FieldOffset(16)] public int3 GridDelta;
        [FieldOffset(28)] public uint Flags;
    }

    /// <summary>Input-to-KCC control signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ControlSignal : ISignal
    {
        [FieldOffset(0)] public uint ControlMask;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float2 Move;
        [FieldOffset(16)] public float2 Look;
        [FieldOffset(24)] public ushort Sequence;
        [FieldOffset(26)] public byte Device;
        [FieldOffset(27)] public byte Flags;
        [FieldOffset(28)] private uint _padTail0;
    }

    /// <summary>Runtime anomaly signal for watchdog systems. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AnomalySignal : ISignal
    {
        [FieldOffset(0)] public uint SystemHash;
        [FieldOffset(4)] public uint AnomalyHash;
        [FieldOffset(8)] public float Scalar;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] private ushort _padTail0;
        [FieldOffset(20)] private uint _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>Compass anomaly proximity signal. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct AnomalyProximitySignal : ISignal
    {
        public const int ExpectedCapacity = 16;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 4;
        public const uint LaneHash = 3986232183u; // FNV32("AnomalyProximitySignal")

        [FieldOffset(0)]
        public AbsoluteUniversePosition SourceAup;
        [FieldOffset(48)]
        public float Proximity01;
        [FieldOffset(52)]
        public float Interference01;
        [FieldOffset(56)]
        public uint Frame;
        [FieldOffset(60)]
        public uint SourceHash;
        [FieldOffset(64)]
        public byte Flags;
        [FieldOffset(65)]
        public byte Reserved0;
        [FieldOffset(66)]
        public ushort Reserved1;
        [FieldOffset(68)]
        public uint Reserved2;
        [FieldOffset(72)]
        public ulong Reserved3;
        [FieldOffset(80)]
        public ulong Reserved4;
        [FieldOffset(88)]
        public ulong Reserved5;
        [FieldOffset(96)]
        public ulong Reserved6;
        [FieldOffset(104)]
        public ulong Reserved7;
        [FieldOffset(112)]
        public ulong Reserved8;
        [FieldOffset(120)]
        public ulong Reserved9;
    }

    /// <summary>Compass recalibration signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CompassCalibratedSignal : ISignal
    {
        [FieldOffset(0)]
        public uint SourceHash;
        [FieldOffset(4)]
        public uint Frame;
        [FieldOffset(8)]
        public float CalibrationQuality01;
        [FieldOffset(12)]
        public byte Flags;
        [FieldOffset(13)]
        public byte Reserved0;
        [FieldOffset(14)]
        public ushort Reserved1;
        [FieldOffset(16)]
        public uint Reserved2;
        [FieldOffset(20)]
        public uint Reserved3;
        [FieldOffset(24)]
        public ulong Reserved4;
    }

    /// <summary>Telemetry anomaly signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TelemetryAnomalySignal : ISignal
    {
        [FieldOffset(0)] public uint SystemHash;
        [FieldOffset(4)] public uint AnomalyHash;
        [FieldOffset(8)] public float Scalar;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] private ushort _padTail0;
        [FieldOffset(20)] private uint _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
        [FieldOffset(32)] private ulong _padTail3;
        [FieldOffset(40)] private ulong _padTail4;
        [FieldOffset(48)] private ulong _padTail5;
        [FieldOffset(56)] private ulong _padTail6;
    }

    /// <summary>Crash/postmortem telemetry signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CrashTelemetrySignal : ISignal
    {
        [FieldOffset(0)] public uint SystemHash;
        [FieldOffset(4)] public uint ReasonHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public int ExitCode;
        [FieldOffset(16)] public int NativeAllocationCount;
        [FieldOffset(20)] public float NativeTrackedBytesMb;
        [FieldOffset(24)] public byte Severity;
        [FieldOffset(25)] public byte Flags;
        [FieldOffset(26)] private ushort _padTail0;
        [FieldOffset(28)] private uint _padTail1;
        [FieldOffset(32)] private ulong _padTail2;
        [FieldOffset(40)] private ulong _padTail3;
        [FieldOffset(48)] private ulong _padTail4;
        [FieldOffset(56)] private ulong _padTail5;
    }

    /// <summary>Habitat construction graph mutation signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HabitatConstructionSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint ModuleHash;
        [FieldOffset(52)] public uint GraphId;
        [FieldOffset(56)] public ushort NodeId;
        [FieldOffset(58)] public byte Operation;
        [FieldOffset(59)] public byte Flags;
        [FieldOffset(60)] private uint _padTail0;
    }

    /// <summary>Tool-to-habitat deconstruction request signal. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct DeconstructRequestSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition TargetAup;
        [FieldOffset(48)] public AbsoluteUniversePosition RayOriginAup;
        [FieldOffset(96)] public uint TargetEntityId;
        [FieldOffset(100)] public uint RequesterEntityId;
        [FieldOffset(104)] public float MaxDistance;
        [FieldOffset(108)] public float3 RayDirection;
        [FieldOffset(120)] public uint Frame;
        [FieldOffset(124)] public byte ToolKind;
        [FieldOffset(125)] public byte Flags;
        [FieldOffset(126)] private ushort _padTail0;
    }

    /// <summary>Habitat deconstruction validation/execution result signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DeconstructResultSignal : ISignal
    {
        public const int ExpectedCapacity = 64;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 146807682u; // FNV32("DeconstructResultSignal")

        [FieldOffset(0)] public AbsoluteUniversePosition TargetAup;
        [FieldOffset(48)] public uint TargetEntityId;
        [FieldOffset(52)] public uint RequesterEntityId;
        [FieldOffset(56)] public ushort RefundItemCount;
        [FieldOffset(58)] public byte Result;
        [FieldOffset(59)] public byte Reason;
        [FieldOffset(60)] public uint Frame;
    }

    /// <summary>Persistence/pipeline deletion marker emitted after a module leaves the graph. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModuleDeconstructSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint ModuleHash;
        [FieldOffset(52)] public uint TargetEntityId;
        [FieldOffset(56)] public ushort NodeId;
        [FieldOffset(58)] public byte Operation;
        [FieldOffset(59)] public byte Flags;
        [FieldOffset(60)] public uint Frame;
    }

    /// <summary>Player vital warning signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VitalWarningSignal : ISignal
    {
        [FieldOffset(0)] public uint WarningHash;
        [FieldOffset(4)] public uint SourceId;
        [FieldOffset(8)] public float Vital01;
        [FieldOffset(12)] public float Severity01;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte Priority;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(22)] private ushort _padTail0;
        [FieldOffset(24)] private ulong _padTail1;
    }

    /// <summary>Crush-depth warning signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CrushWarningSignal : ISignal
    {
        [FieldOffset(0)] public uint WarningHash;
        [FieldOffset(4)] public uint SourceId;
        [FieldOffset(8)] public float DepthMeters;
        [FieldOffset(12)] public float CrushLimitMeters;
        [FieldOffset(16)] public float Severity01;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public byte Priority;
        [FieldOffset(25)] public byte Flags;
        [FieldOffset(26)] private ushort _padTail0;
        [FieldOffset(28)] private uint _padTail1;
    }

    /// <summary>Hash-addressed subtitle request signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SubtitleSignal : ISignal
    {
        [FieldOffset(0)] public uint SubtitleHash;
        [FieldOffset(4)] public uint SpeakerHash;
        [FieldOffset(8)] public float DurationSeconds;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Priority;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] private ushort _padTail0;
        [FieldOffset(20)] private uint _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>Submarine vocal warning signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VocalWarningSignal : ISignal
    {
        [FieldOffset(0)] public uint WarningHash;
        [FieldOffset(4)] public uint SourceId;
        [FieldOffset(8)] public float Severity01;
        [FieldOffset(12)] public float CooldownSeconds;
        [FieldOffset(16)] public byte Priority;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] private ushort _padTail0;
        [FieldOffset(20)] private uint _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>Hash-addressed protagonist voice cue consumed by the vocal synthesis MMF decoder. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VocalCueSignal : ISignal
    {
        [FieldOffset(0)] public uint PhraseHashID;
        [FieldOffset(4)] public int Priority;
        [FieldOffset(8)] public float VolumeScalar;
        [FieldOffset(12)] public float PlaybackSpeed;
        [FieldOffset(16)] public float RadioDistortion01;
        [FieldOffset(20)] public float SpatialBlend01;
        [FieldOffset(24)] public long SourceAupGridX;
        [FieldOffset(32)] public long SourceAupGridY;
        [FieldOffset(40)] public long SourceAupGridZ;
        [FieldOffset(48)] public float SourceAupLocalX;
        [FieldOffset(52)] public float SourceAupLocalY;
        [FieldOffset(56)] public float SourceAupLocalZ;
        [FieldOffset(60)] public uint Flags;
    }

    /// <summary>Editor data reload signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DataReloadSignal : ISignal
    {
        [FieldOffset(0)] public uint DataHash;
        [FieldOffset(4)] public uint CategoryHash;
        [FieldOffset(8)] public uint Revision;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Flags;
        [FieldOffset(17)] private byte _padTail0;
        [FieldOffset(18)] private ushort _padTail1;
        [FieldOffset(20)] private uint _padTail2;
        [FieldOffset(24)] private ulong _padTail3;
    }

    /// <summary>Memory pressure signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MemoryPressureSignal : ISignal
    {
        [FieldOffset(0)] public long ReservedMemoryBytes;
        [FieldOffset(8)] public long PhysicalMemoryBytes;
        [FieldOffset(16)] public float UsageRatio;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public byte Severity;
        [FieldOffset(25)] public byte Flags;
        [FieldOffset(26)] private ushort _padTail0;
        [FieldOffset(28)] private uint _padTail1;
    }

    /// <summary>GlobalDataVault relocation and swap-pop notice for systems caching vault descriptors or indices. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MemoryAddressShiftSignal : ISignal
    {
        public const byte FlagMemMove = 1 << 0;
        public const byte FlagFenceProtected = 1 << 1;
        public const byte FlagSwapPopIndexMove = 1 << 2;

        [FieldOffset(0)] public long OldOffsetBytes;
        [FieldOffset(8)] public long NewOffsetBytes;
        [FieldOffset(16)] public int BufferId;
        [FieldOffset(20)] public int ByteLength;
        [FieldOffset(24)] public uint Version;
        [FieldOffset(28)] public byte Flags;
        [FieldOffset(29)] public byte SystemId;
        [FieldOffset(30)] private ushort _pad0;
        [FieldOffset(32)] public int OldIndex;
        [FieldOffset(36)] public int NewIndex;
        [FieldOffset(40)] public uint MovedEntityId;
        [FieldOffset(44)] public uint SourceFrame;
        [FieldOffset(48)] public uint SourceHash;
        [FieldOffset(52)] public uint CompactedCount;
        [FieldOffset(56)] private ulong _pad1;
    }

    /// <summary>Runtime mip/resolution residency change signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ResolutionChangedSignal : ISignal
    {
        public const byte ReasonVramRedline = 1;
        public const byte ReasonVramRecovered = 2;
        public const byte ReasonRenderScaleDropped = 3;
        public const byte ReasonRenderScaleRaised = 4;
        public const byte FlagTextureMipLimit = 1 << 0;
        public const byte FlagRenderScale = 1 << 1;
        public const byte FlagStpActive = 1 << 2;

        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint SourceHash;
        [FieldOffset(8)] public int OldMipLimit;
        [FieldOffset(12)] public int NewMipLimit;
        [FieldOffset(16)] public float VramUsedMb;
        [FieldOffset(20)] public byte Reason;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(22)] private ushort _padTail0;
        [FieldOffset(24)] private ulong _padTail1;
    }

    /// <summary>Homeostasis state broadcast. Critical state is the SHI_Critical equivalent. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SystemHealthIndexSignal : ISignal
    {
        public const byte StateStable = 0;
        public const byte StateWarning = 1;
        public const byte StateCritical = 2;
        public const byte FlagAdrenaline = 1 << 0;

        [FieldOffset(0)] public float Health01;
        [FieldOffset(4)] public float Pressure01;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint SourceHash;
        [FieldOffset(16)] public byte State;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] private ushort _padTail0;
        [FieldOffset(20)] private uint _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>CPU worker-starvation broadcast emitted when a non-critical job admission is denied. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CpuStarvationSignal : ISignal
    {
        [FieldOffset(0)] public uint JobHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float EstimatedCostMs;
        [FieldOffset(12)] public float RemainingBudgetMs;
        [FieldOffset(16)] public int CriticalDebtFrames;
        [FieldOffset(20)] public byte Lane;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(22)] private ushort _padTail0;
        [FieldOffset(24)] private ulong _padTail1;
    }

    /// <summary>Acoustic ping broadcast signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AcousticPingSignal : ISignal
    {
        public const int ExpectedCapacity = 128;
        public const int MaxFrameSignals = 128;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 2525108346u; // FNV32("AcousticPingSignal")
        public const byte ChannelActiveSonar = 2;
        public const byte ChannelGloveScrape = 3;
        public const byte ChannelFabricScrape = ChannelGloveScrape;
        public const byte ChannelMetalStress = 4;
        public const byte ChannelLeviathanRoar = 5;
        public const byte ChannelLootZip = 6;
        public const byte ChannelJawSnap = 7;
        public const byte FlagActiveSonar = 1;
        public const byte FlagGloveScrape = 1 << 1;
        public const byte FlagFabricScrape = FlagGloveScrape;
        public const byte FlagLeviathanRoar = 1 << 2;
        public const byte FlagLootZip = 1 << 3;
        public const byte FlagJawSnap = 1 << 4;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public byte Channel;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] public ushort Reserved0;
    }

    /// <summary>Player movement acoustic broadcast signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MovementAcousticSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float Volume;
        [FieldOffset(52)] public float VelocitySq;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public byte LocomotionMode;
        [FieldOffset(61)] public byte SurfaceMode;
        [FieldOffset(62)] public byte Flags;
        [FieldOffset(63)] private byte _padTail0;
    }

    /// <summary>GPU swarm dispersion broadcast signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SwarmDispersedSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public ushort EstimatedBoidCount;
        [FieldOffset(62)] public byte Flags;
        [FieldOffset(63)] public byte QualityTier;
    }

    /// <summary>World chunk hydration broadcast consumed by data-only ecology systems. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SectorResidencyHydratedSignal : ISignal
    {
        public const byte FlagProxyFallback = 1;
        public const byte FlagPinned = 1 << 1;

        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public long ChunkId;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public ushort RadiusMetersQ;
        [FieldOffset(62)] public byte Flags;
        [FieldOffset(63)] public byte ResidencyState;
    }

    /// <summary>World chunk dehydration broadcast consumed by data-only ecology systems. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SectorDehydratedSignal : ISignal
    {
        public const byte FlagProxyFallback = 1;
        public const byte FlagPinned = 1 << 1;

        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public long ChunkId;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public ushort RadiusMetersQ;
        [FieldOffset(62)] public byte Flags;
        [FieldOffset(63)] public byte ResidencyState;
    }

    /// <summary>Chunk dehydration persistence trigger. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ChunkDehydratedSignal : ISignal
    {
        public const byte FlagProxyFallback = SectorDehydratedSignal.FlagProxyFallback;
        public const byte FlagPinned = SectorDehydratedSignal.FlagPinned;

        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public long SectorHash;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public ushort RadiusMetersQ;
        [FieldOffset(62)] public byte Flags;
        [FieldOffset(63)] public byte ResidencyState;
    }

    /// <summary>Sonar ping broadcast signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SonarPingSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float RadiusMeters;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public byte Flags;
        [FieldOffset(61)] private byte _padTail0;
        [FieldOffset(62)] private ushort _padTail1;
    }

    /// <summary>Hypoxia warning signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HypoxiaSignal : ISignal
    {
        [FieldOffset(0)] public float Oxygen01;
        [FieldOffset(4)] public float SecondsRemaining;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] private ushort _padTail0;
        [FieldOffset(20)] private uint _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>Oxygen critical signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct OxygenCriticalSignal : ISignal
    {
        [FieldOffset(0)] public float Oxygen01;
        [FieldOffset(4)] public float SecondsRemaining;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] private ushort _padTail0;
        [FieldOffset(20)] private uint _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>Interaction UI show/hide signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct InteractionUiSignal : ISignal
    {
        public const int ExpectedCapacity = 128;
        public const int MaxFrameSignals = 128;
        public const int LowTierFrameSignals = 32;
        public const uint LaneHash = 38002005u; // FNV32("InteractionUiSignal")

        [FieldOffset(0)] public AbsoluteUniversePosition TargetAup;
        [FieldOffset(48)] public uint TargetHash;
        [FieldOffset(52)] public uint ToolHash;
        [FieldOffset(56)] public byte State;
        [FieldOffset(57)] public byte Flags;
        [FieldOffset(58)] private ushort _padTail0;
        [FieldOffset(60)] private uint _padTail1;
    }

    /// <summary>UI layout rescale request emitted after staged font swaps. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct UIRescaleRequestSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public ushort Reason;
        [FieldOffset(10)] public ushort Language;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float FontScale;
        [FieldOffset(20)] private uint _padTail0;
        [FieldOffset(24)] private ulong _padTail1;
    }

    /// <summary>Fluid incursion compartment signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FluidIncursionSignal : ISignal
    {
        public const int ExpectedCapacity = 64;
        public const int MaxFrameSignals = 128;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 2553418623u; // FNV32("FluidIncursionSignal")

        [FieldOffset(0)] public AbsoluteUniversePosition LeakAup;
        [FieldOffset(48)] public uint CompartmentId;
        [FieldOffset(52)] public float FloodLevel01;
        [FieldOffset(56)] public float FlowRate01;
        [FieldOffset(60)] public byte Flags;
        [FieldOffset(61)] private byte _padTail0;
        [FieldOffset(62)] private ushort _padTail1;
    }

    /// <summary>Submarine dynamic flood mass-state signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SubmarineFloodStateSignal : ISignal
    {
        public const byte FlagHasFloodMass = 1 << 0;
        public const byte FlagCriticalFlood = 1 << 1;
        public const byte FlagInvalid = 1 << 7;

        [FieldOffset(0)] public float3 DynamicCenterOfMassLocal;
        [FieldOffset(12)] public float3 DynamicCenterOfMassOffsetLocal;
        [FieldOffset(24)] public float TotalWaterMassKg;
        [FieldOffset(28)] public float BaseMassKg;
        [FieldOffset(32)] public float FillRatio01;
        [FieldOffset(36)] public float AngularDragMultiplier;
        [FieldOffset(40)] public uint SourceBodyId;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] public ushort RoomCount;
        [FieldOffset(50)] public byte MathLod;
        [FieldOffset(51)] public byte Flags;
        [FieldOffset(52)] private uint _padTail0;
        [FieldOffset(56)] private ulong _padTail1;
    }

    /// <summary>Fluid density transition signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FluidDensityChangedSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float DensityMultiplier;
        [FieldOffset(52)] public float BrineHeightY;
        [FieldOffset(56)] public float SubmersionSeconds;
        [FieldOffset(60)] public byte Flags;
        [FieldOffset(61)] public byte FluidKind;
        [FieldOffset(62)] public ushort SectorHash;
    }

    /// <summary>Fluid pipe rupture signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PipeRuptureSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition RuptureAup;
        [FieldOffset(48)] public uint NetworkId;
        [FieldOffset(52)] public uint NodeId;
        [FieldOffset(56)] public float PressureKPa;
        [FieldOffset(60)] public byte ContentKind;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] public short RoomIndex;
    }

    /// <summary>Spectrum scan frequency signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SpectrumScanSignal : ISignal
    {
        [FieldOffset(0)] public uint ScanId;
        [FieldOffset(4)] public float FrequencyHz;
        [FieldOffset(8)] public float Amplitude01;
        [FieldOffset(12)] public float Noise01;
        [FieldOffset(16)] public byte Band;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] private ushort _padTail0;
        [FieldOffset(20)] private uint _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>Rigidbody sleep-state signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RigidbodySleepSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint BodyId;
        [FieldOffset(52)] public float DistanceMeters;
        [FieldOffset(56)] public byte SleepState;
        [FieldOffset(57)] public byte Flags;
        [FieldOffset(58)] private ushort _padTail0;
        [FieldOffset(60)] private uint _padTail1;
    }

    /// <summary>Scanner active-state signal consumed by diegetic tuning UI. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ScannerToolActiveSignal : ISignal
    {
        [FieldOffset(0)] public uint ToolHash;
        [FieldOffset(4)] public uint ArtifactHash;
        [FieldOffset(8)] public uint BlueprintHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public float Progress01;
        [FieldOffset(20)] public float Battery01;
        [FieldOffset(24)] public byte Active;
        [FieldOffset(25)] public byte Stage;
        [FieldOffset(26)] public byte Flags;
        [FieldOffset(27)] public byte QualityTier;
        [FieldOffset(28)] private uint _padTail0;
    }

    /// <summary>Scan-complete signal for PDA/lore unlock consumers. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ScanCompleteSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint EntryHash;
        [FieldOffset(52)] public uint ScanId;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public byte ReconKind;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] private ushort _padTail0;
    }

    /// <summary>Lore-fragment scan commit signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LoreFragmentScannedSignal : ISignal
    {
        public const byte FlagPairedScanComplete = 1 << 0;
        public const byte FlagHasAup = 1 << 1;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint Hash;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public byte Flags;
        [FieldOffset(61)] private byte _padTail0;
        [FieldOffset(62)] private ushort _padTail1;
    }

    /// <summary>AppliedLore terminal preview request consumed by TerminalOS in VISUAL_SYNC. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AppliedLoreTerminalPreviewSignal : ISignal
    {
        public const int ExpectedCapacity = 64;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 8;
        public const uint LaneHash = 0x41545056u; // ATPV
        public const byte FlagHasTerminalHash = 1 << 0;

        [FieldOffset(0)] public uint PacketHash;
        [FieldOffset(4)] public uint LocaleHash;
        [FieldOffset(8)] public uint TerminalHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public int TerminalIndex;
        [FieldOffset(20)] public uint SourceHash;
        [FieldOffset(24)] public byte Surface;
        [FieldOffset(25)] public byte Flags;
        [FieldOffset(26)] private ushort _padTail0;
        [FieldOffset(28)] private uint _padTail1;
    }

    /// <summary>Blueprint unlock signal for crafting and PDA consumers. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BlueprintUnlockedSignal : ISignal
    {
        [FieldOffset(0)] public uint EntityHash;
        [FieldOffset(4)] public uint BlueprintHash;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Category;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] private ushort _padTail0;
        [FieldOffset(20)] private uint _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>Crafting start signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CraftingStartedSignal : ISignal
    {
        [FieldOffset(0)] public uint FabricatorHash;
        [FieldOffset(4)] public uint RecipeHash;
        [FieldOffset(8)] public uint ResultItemHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public ushort Multiplier;
        [FieldOffset(18)] public byte Flags;
        [FieldOffset(19)] private byte _padTail0;
        [FieldOffset(20)] private uint _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>Crafting completion signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CraftingCompletedSignal : ISignal
    {
        [FieldOffset(0)] public uint FabricatorHash;
        [FieldOffset(4)] public uint RecipeHash;
        [FieldOffset(8)] public uint ResultItemHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public ushort Quantity;
        [FieldOffset(18)] public byte Flags;
        [FieldOffset(20)] public uint Sequence;
        [FieldOffset(24)] private ulong _padTail0;
    }

    /// <summary>Authoritative active tool state signal consumed by diegetic tool screens. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ToolStateChangedSignal : ISignal
    {
        public const byte FlagEquipped = 1 << 0;
        public const byte FlagVisible = 1 << 1;
        public const byte FlagLowTierFallback = 1 << 2;

        [FieldOffset(0)] public uint ToolHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Battery01;
        [FieldOffset(12)] public float Heat01;
        [FieldOffset(16)] public float DistanceMeters;
        [FieldOffset(20)] public float Durability01;
        [FieldOffset(24)] public uint StatusMask;
        [FieldOffset(28)] public ushort AmmoUnits;
        [FieldOffset(30)] public byte Flags;
        [FieldOffset(31)] public byte ToolTypeId;
    }

    /// <summary>Player quick-slot assignment and active slot dirty signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ToolLoadoutChangedSignal : ISignal
    {
        public const ushort NoActiveSlot = ushort.MaxValue;
        public const byte ReasonActiveSlotChanged = 1;
        public const byte ReasonAssignmentsChanged = 2;
        public const byte FlagHasActiveTool = 1 << 0;
        public const byte FlagSwapInProgress = 1 << 1;

        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Sequence;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint ActiveToolHash;
        [FieldOffset(16)] public uint AssignedSlotMask;
        [FieldOffset(20)] public ushort ActiveSlot;
        [FieldOffset(22)] public ushort SlotCount;
        [FieldOffset(24)] public byte Reason;
        [FieldOffset(25)] public byte Flags;
        [FieldOffset(26)] private ushort _padTail0;
        [FieldOffset(28)] private uint _padTail1;
    }

    /// <summary>Tool acoustic state signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ToolAcousticSignal : ISignal
    {
        public const int ExpectedCapacity = 128;
        public const int MaxFrameSignals = 128;
        public const int LowTierFrameSignals = 32;
        public const uint LaneHash = 1213288304u; // FNV32("ToolAcousticSignal")
        public const byte StateLaserLoop = 1;
        public const byte StateDataGhost = 7;
        public const byte FlagLooping = 1 << 0;
        public const byte FlagNarrativeGhost = 1 << 1;
        public const byte FlagCorrupted = 1 << 2;

        [FieldOffset(0)] public uint ToolHash;
        [FieldOffset(4)] public uint TargetHash;
        [FieldOffset(8)] public float Progress01;
        [FieldOffset(12)] public float PitchScale;
        [FieldOffset(16)] public float Intensity01;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public byte State;
        [FieldOffset(25)] public byte Flags;
        [FieldOffset(26)] private ushort _padTail0;
        [FieldOffset(28)] private uint _padTail1;
    }

    /// <summary>Power drain intent signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PowerDrainSignal : ISignal
    {
        [FieldOffset(0)] public uint ConsumerHash;
        [FieldOffset(4)] public uint NetworkHash;
        [FieldOffset(8)] public float Watts;
        [FieldOffset(12)] public float Progress01;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte Reason;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(22)] private ushort _padTail0;
        [FieldOffset(24)] private ulong _padTail1;
    }

    /// <summary>OpenXR/input bridge trigger signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ToolTriggerSignal : ISignal
    {
        [FieldOffset(0)] public float Strength;
        [FieldOffset(4)] public float SecondaryStrength;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint ControllerMask;
        [FieldOffset(16)] public ushort Sequence;
        [FieldOffset(18)] public byte DominantController;
        [FieldOffset(19)] public byte Flags;
        [FieldOffset(20)] private uint _padTail0;
        [FieldOffset(24)] private ulong _padTail1;
    }

    /// <summary>Storage IO backpressure scalar for movement, PDA, VFX, and telemetry consumers. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct StorageDebtSignal : ISignal
    {
        public const byte HighDebtFlag = 1 << 0;
        public const byte DataLinkDegradedFlag = 1 << 1;
        public const byte CriticalHoleFlag = 1 << 2;
        public const byte ProxyFallbackFlag = 1 << 3;

        [FieldOffset(0)] public float Debt01;
        [FieldOffset(4)] public float LatencyEwmaMs;
        [FieldOffset(8)] public float OldestPendingMs;
        [FieldOffset(12)] public float CriticalHoleDebtMs;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint Sequence;
        [FieldOffset(24)] public ushort PendingLoads;
        [FieldOffset(26)] public byte Flags;
        [FieldOffset(27)] private byte _padTail0;
        [FieldOffset(28)] private uint _padTail1;
    }

    /// <summary>Visual-only streaming turbulence cue for masking high IO debt. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct StreamingTurbulenceSignal : ISignal
    {
        [FieldOffset(0)] public float Intensity01;
        [FieldOffset(4)] public float Debt01;
        [FieldOffset(8)] public float DurationSeconds;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public uint SourceHash;
        [FieldOffset(20)] public uint Sequence;
        [FieldOffset(24)] private ulong _padTail0;
    }

    /// <summary>Orbital prologue re-entry phase packet. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AtmosphericReentrySignal : ISignal
    {
        public const byte PhaseApproach = 1;
        public const byte PhasePlasma = 2;
        public const byte PhaseWhiteout = 3;
        public const byte FlagAuthoritativeHeat = 1 << 0;
        public const byte FlagWhiteoutRequested = 1 << 1;

        [FieldOffset(0)] public AbsoluteUniversePosition CapsuleAup;
        [FieldOffset(48)] public float AltitudeMeters;
        [FieldOffset(52)] public float UniverseVelocityMetersPerSecond;
        [FieldOffset(56)] public float Heat01;
        [FieldOffset(60)] public ushort Sequence;
        [FieldOffset(62)] public byte Flags;
        [FieldOffset(63)] public byte Phase;
    }

    /// <summary>Orbital prologue acoustic stress packet. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ReentryAcousticStressSignal : ISignal
    {
        public const int ExpectedCapacity = 16;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 4;
        public const uint LaneHash = 0xA6505E31u; // FNV32("ReentryAcousticStressSignal")
        public const byte PhaseSpace = 1;
        public const byte PhasePlasma = 2;
        public const byte PhaseWhiteout = 3;
        public const byte PhaseSplashdown = 4;
        public const byte FlagAuthoritativeFilter = 1 << 0;
        public const byte FlagSplashdown = 1 << 1;
        public const byte FlagNonFiniteGuard = 1 << 2;

        [FieldOffset(0)] public float Stress01;
        [FieldOffset(4)] public float Heat01;
        [FieldOffset(8)] public float UniverseVelocityMetersPerSecond;
        [FieldOffset(12)] public float LowPassCutoffHz;
        [FieldOffset(16)] public float LfeGain01;
        [FieldOffset(20)] public float GranularStress01;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public ushort Sequence;
        [FieldOffset(30)] public byte Flags;
        [FieldOffset(31)] public byte Phase;
    }

    /// <summary>Orbital prologue whiteout completion packet. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PrologueCompleteSignal : ISignal
    {
        public const byte PhaseWhiteout = 1;
        public const byte PhaseOceanHandoff = 2;
        public const byte FlagForceWhiteout = 1 << 0;

        [FieldOffset(0)] public AbsoluteUniversePosition CapsuleAup;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public float WhiteoutHoldSeconds;
        [FieldOffset(56)] public uint SourceHash;
        [FieldOffset(60)] public ushort Sequence;
        [FieldOffset(62)] public byte Flags;
        [FieldOffset(63)] public byte Phase;
    }

    /// <summary>Deterministic seismic radial-wave signal. Size: 96 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct SeismicSignal : ISignal
    {
        public const int ExpectedCapacity = 64;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 0x4A180124u;
        public const byte FlagRadialWave = 1 << 7;
        public const byte FlagPresentationOnly = 1 << 6;
        public const byte LegacyQualityMask = 0x0F;

        [FieldOffset(0)] public float3 Direction;
        [FieldOffset(12)] public float Intensity01;
        [FieldOffset(16)] public float CameraJitter01;
        [FieldOffset(20)] public float AudioIntensity01;
        [FieldOffset(24)] public float ThermalEruptionProbabilityScalar;
        [FieldOffset(28)] public ushort Sequence;
        [FieldOffset(30)] public byte DepthFlags;
        [FieldOffset(31)] public byte Flags;
        [FieldOffset(32)] public double3 EpicenterAUP;
        [FieldOffset(56)] public float CurrentRadiusMeters;
        [FieldOffset(60)] public float PWaveRadiusMeters;
        [FieldOffset(64)] public float SWaveRadiusMeters;
        [FieldOffset(68)] public float MagnitudeRichter;
        [FieldOffset(72)] public float PWaveAmplitude01;
        [FieldOffset(76)] public float SWaveAmplitude01;
        [FieldOffset(80)] public uint SourceHash;
        [FieldOffset(84)] public uint Frame;
        [FieldOffset(88)] public uint EventTypeHash;
        [FieldOffset(92)] public uint Reserved0;
    }

    /// <summary>Weather strength signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct WeatherStrengthSignal : ISignal
    {
        [FieldOffset(0)] public float Strength01;
        [FieldOffset(4)] public float FlowFieldScale;
        [FieldOffset(8)] public uint WeatherHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Flags;
        [FieldOffset(17)] private byte _padTail0;
        [FieldOffset(18)] private ushort _padTail1;
        [FieldOffset(20)] private uint _padTail2;
        [FieldOffset(24)] private ulong _padTail3;
    }

    /// <summary>Item decay/broken signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ItemDecaySignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint ItemHash;
        [FieldOffset(52)] public float Durability01;
        [FieldOffset(56)] public ushort OwnerSlot;
        [FieldOffset(58)] public byte State;
        [FieldOffset(59)] public byte Flags;
        [FieldOffset(60)] private uint _padTail0;
    }

    public static class LightLevelSignalSampleKinds
    {
        public const byte CaveVoxelSdf = 1;
    }

    public static class LightLevelSignalFlags
    {
        public const byte ValidSample = 1 << 0;
    }

    /// <summary>Voxel lighting-to-physiology light sample. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct LightLevelSignal : ISignal
    {
        [FieldOffset(0)] public float LightLevel01;
        [FieldOffset(4)] public float Darkness01;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte SampleKind;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] private ushort _padTail0;
        [FieldOffset(20)] private uint _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    public static class SubmarineLightsChangedSignalOperations
    {
        public const byte Remove = 0;
        public const byte Upsert = 1;
        public const byte ClearSource = 2;
    }

    public static class SubmarineLightsChangedSignalFlags
    {
        public const byte Powered = 1 << 0;
        public const byte BrownoutSuppressed = 1 << 1;
    }

    /// <summary>AUP-safe headlight registry delta. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct SubmarineLightsChangedSignal : ISignal
    {
        public const int ExpectedCapacity = 64;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 887228434u; // FNV32("SubmarineLightsChangedSignal")

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float3 Forward;
        [FieldOffset(60)] public float RangeMeters;
        [FieldOffset(64)] public float Intensity;
        [FieldOffset(68)] public uint SourceId;
        [FieldOffset(72)] public ushort Slot;
        [FieldOffset(74)] public byte Operation;
        [FieldOffset(75)] public byte Flags;
        [FieldOffset(76)] public float SpotOuterCos;
        [FieldOffset(80)] public ulong Reserved0;
        [FieldOffset(88)] public ulong Reserved1;
        [FieldOffset(96)] public ulong Reserved2;
        [FieldOffset(104)] public ulong Reserved3;
        [FieldOffset(112)] public ulong Reserved4;
        [FieldOffset(120)] public ulong Reserved5;
    }

    public static class FaunaStateChangedSignalKinds
    {
        public const byte Blind = 1;
        public const byte Mutated = 2;
        public const byte Strike = 3;
    }

    public static class FaunaStateChangedSignalFlags
    {
        public const byte StateActive = 1 << 0;
    }

    /// <summary>Fauna high-level state transition signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FaunaStateChangedSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint SpeciesHash;
        [FieldOffset(52)] public uint StateFlags;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public ushort Slot;
        [FieldOffset(62)] public byte StateKind;
        [FieldOffset(63)] public byte Flags;
    }

    /// <summary>Authoritative player physiology state signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PhysiologyStateSignal : ISignal
    {
        public const int ExpectedCapacity = 64;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 32;
        public const uint LaneHash = 0x50485953u; // PHYS
        public const uint SourceShinobuPhysiology = 0x53483231u; // SH21
        public const byte CauseDecompression = 3;
        public const byte CauseGasToxicity = 4;
        public const uint StatusGasHypoxia = 1u << 12;
        public const uint StatusGasHyperoxia = 1u << 13;
        public const uint StatusGasCarbonDioxideToxicity = 1u << 14;
        public const uint StatusGasCnsOxygenToxicity = 1u << 15;
        public const uint StatusGasFatalToxicity = 1u << 16;
        public const uint GasStatusMask = StatusGasHypoxia |
                                          StatusGasHyperoxia |
                                          StatusGasCarbonDioxideToxicity |
                                          StatusGasCnsOxygenToxicity |
                                          StatusGasFatalToxicity;

        [FieldOffset(0)] public float PlayerStress01;
        [FieldOffset(4)] public float O2DrainMultiplier;
        [FieldOffset(8)] public float Recovery01;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Cause;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] public byte GasCnsSeverity;
        [FieldOffset(19)] public byte GasCarbonDioxideSeverity;
        [FieldOffset(20)] public float Supersaturation01;
        [FieldOffset(24)] public float Narcosis01;
        [FieldOffset(28)] public float AmbientPressureAtm;
        [FieldOffset(32)] public float NitrogenLoadAtm;
        [FieldOffset(36)] public float AscentRateMetersPerSecond;
        [FieldOffset(40)] public uint TissueOverMValueMask;
        [FieldOffset(44)] public uint SourceHash;
        [FieldOffset(48)] public int EntityIndex;
        [FieldOffset(52)] public byte ActiveCompartments;
        [FieldOffset(53)] public byte FatalSeverity;
        [FieldOffset(54)] private ushort _pad0;
        [FieldOffset(56)] public uint StatusFlags;
        [FieldOffset(60)] private uint _padTail0;
    }

    /// <summary>Player stress signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PlayerStressSignal : ISignal
    {
        [FieldOffset(0)] public float Stress01;
        [FieldOffset(4)] public float OxygenDrainScale;
        [FieldOffset(8)] public float AggressionScale;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Cause;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] private ushort _padTail0;
        [FieldOffset(20)] private uint _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>Player trauma escalation signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TraumaSignal : ISignal
    {
        [FieldOffset(0)] public uint TraumaHash;
        [FieldOffset(4)] public float Stress01;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public byte TraumaKind;
        [FieldOffset(13)] public byte Severity;
        [FieldOffset(14)] public byte Flags;
        [FieldOffset(15)] private byte _padTail0;
        [FieldOffset(16)] private ulong _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>Cache-contiguous combat damage lane payload. Size: 64 bytes.</summary>
    /// <summary>Camera position lane for foveated simulation. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CameraPositionSignal : ISignal
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public float3 Forward;
        [FieldOffset(28)] public byte Flags;
        [FieldOffset(29)] private byte _padTail0;
        [FieldOffset(30)] private ushort _padTail1;
    }

    /// <summary>Camera frustum lane for foveated simulation. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CameraFrustumSignal : ISignal
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Forward;
        [FieldOffset(24)] public float3 Up;
        [FieldOffset(36)] public float FieldOfViewDegrees;
        [FieldOffset(40)] public float NearClipMeters;
        [FieldOffset(44)] public float FarClipMeters;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public byte Flags;
        [FieldOffset(53)] private byte _padTail0;
        [FieldOffset(54)] private ushort _padTail1;
        [FieldOffset(56)] private ulong _padTail2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CombatDamageSignal : ISignal
    {
        public const int ExpectedCapacity = 256;
        public const int MaxFrameSignals = 128;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 3474161304u; // FNV32("CombatDamageSignal")
        public const byte LegacyMirrorFlag = 1 << 0;
        public const byte DirectRuntimeFlag = 1 << 1;
        public const byte VisualOnlyFlag = 1 << 2;

        [FieldOffset(0)] public double3 ImpactAup;
        [FieldOffset(24)] public float3 Direction;
        [FieldOffset(36)] public float Magnitude;
        [FieldOffset(40)] public uint DamageType;
        [FieldOffset(44)] public uint TargetHash;
        [FieldOffset(48)] public uint SourceHash;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public ushort SourceId;
        [FieldOffset(58)] public ushort TargetId;
        [FieldOffset(60)] public byte Channel;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] public byte IntegrityDelta;
        [FieldOffset(63)] public byte Reserved0;
    }

    public static class CombatDamageSignalCodec
    {
        private const double MaxAupExtentMeters = 100000.0d;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 FromRuntimePoint(float3 runtimePoint)
        {
            return TryResolveRuntimePointAup(runtimePoint, out double3 impactAup) ? impactAup : double3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 FromRuntimePoint(Vector3 runtimePoint)
        {
            return FromRuntimePoint(new float3(runtimePoint.x, runtimePoint.y, runtimePoint.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryToRuntimePoint(in CombatDamageSignal signal, out float3 runtimePoint)
        {
            runtimePoint = default;
            if (!IsFiniteAup(signal.ImpactAup))
                return false;

            Vector3 runtime = global::Hecton8.Core.HectonFloatingOrigin.ToRuntimePosition(signal.ImpactAup);
            runtimePoint = new float3(runtime.x, runtime.y, runtime.z);
            return math.all(math.isfinite(runtimePoint));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ToRuntimePointOrZero(in CombatDamageSignal signal)
        {
            return TryToRuntimePoint(in signal, out float3 runtimePoint) ? runtimePoint : float3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFiniteAup(double3 aup)
        {
            return math.all(math.isfinite(aup)) &&
                   math.abs(aup.x) <= MaxAupExtentMeters &&
                   math.abs(aup.y) <= MaxAupExtentMeters &&
                   math.abs(aup.z) <= MaxAupExtentMeters;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveRuntimePointAup(float3 runtimePoint, out double3 impactAup)
        {
            impactAup = double3.zero;
            if (!math.all(math.isfinite(runtimePoint)))
                return false;

            double3 originAup = global::Hecton8.Core.HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(originAup)))
                return false;

            double3 resolvedAup = originAup + new double3(runtimePoint.x, runtimePoint.y, runtimePoint.z);
            if (!IsFiniteAup(resolvedAup))
                return false;

            impactAup = resolvedAup;
            return true;
        }
    }

    /// <summary>Visual hull dent notification lane for audio groans and non-authoritative feedback. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HullDeformedSignal : ISignal
    {
        public const int ExpectedCapacity = 64;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 4279913826u; // FNV32("HullDeformedSignal")
        public const byte LowTierVisualOnlyFlag = 1 << 0;
        public const byte LegacyLocalPointFlag = 1 << 1;

        [FieldOffset(0)] public float3 LocalPoint;
        [FieldOffset(12)] public float Radius;
        [FieldOffset(16)] public float Depth;
        [FieldOffset(20)] public float Intensity01;
        [FieldOffset(24)] public uint TargetHash;
        [FieldOffset(28)] public uint SourceHash;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public ushort TargetId;
        [FieldOffset(38)] public ushort SourceId;
        [FieldOffset(40)] public byte ActiveDentCount;
        [FieldOffset(41)] public byte Flags;
        [FieldOffset(42)] public byte QualityTier;
        [FieldOffset(43)] public byte Channel;
        [FieldOffset(44)] public uint DamageType;
        [FieldOffset(48)] private ulong _padTail0;
        [FieldOffset(56)] private ulong _padTail1;
    }

    /// <summary>Authoritative hull dent repair completion lane for atmosphere sealing and repair feedback. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HullRepairedSignal : ISignal
    {
        public const int ExpectedCapacity = 64;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 2577695098u; // FNV32("HullRepairedSignal")
        public const byte CompletedFlag = 1 << 0;
        public const byte SurvivalPressureVisualOnlyFlag = 1 << 1;
        public const byte LowTierVisualOnlyFlag = SurvivalPressureVisualOnlyFlag;

        [FieldOffset(0)] public AbsoluteUniversePosition HitAup;
        [FieldOffset(48)] public int RoomId;
        [FieldOffset(52)] public uint SourceHash;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public byte DentIndex;
        [FieldOffset(61)] public byte DentsRepairedCount;
        [FieldOffset(62)] public byte QualityWeightQ8;
        [FieldOffset(62)] public byte QualityTier;
        [FieldOffset(63)] public byte Flags;
    }

    /// <summary>Habitat module deformation reached the compromise threshold. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BaseModuleCompromisedSignal : ISignal
    {
        public const int ExpectedCapacity = 64;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 3041159082u; // FNV32("BaseModuleCompromisedSignal")
        public const ushort MaxDeformationFlag = 1 << 0;
        public const ushort SurvivalPressureVisualOnlyFlag = 1 << 1;
        public const ushort LowTierVisualOnlyFlag = SurvivalPressureVisualOnlyFlag;

        [FieldOffset(0)] public float3 ModuleCenter;
        [FieldOffset(12)] public float Stress01;
        [FieldOffset(16)] public float PeakStress01;
        [FieldOffset(20)] public float DepthMeters;
        [FieldOffset(24)] public uint NodeId;
        [FieldOffset(28)] public uint ModuleHash;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public uint Sequence;
        [FieldOffset(40)] public ushort SourceId;
        [FieldOffset(42)] public ushort Flags;
        [FieldOffset(44)] public byte StressIndex;
        [FieldOffset(45)] public byte QualityWeightQ8;
        [FieldOffset(45)] public byte QualityTier;
        [FieldOffset(46)] public ushort Reserved0;
        [FieldOffset(48)] private ulong _padTail0;
        [FieldOffset(56)] private ulong _padTail1;
    }

    /// <summary>Player entered a habitat/base volume. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PlayerBaseEnterSignal : ISignal
    {
        public const ushort DirectPlayerInsideFlag = 1 << 0;
        public const ushort SanitizedBaseCenterFlag = 1 << 15;

        [FieldOffset(0)] public AbsoluteUniversePosition BaseCenterAup;
        [FieldOffset(48)] public int BaseId;
        [FieldOffset(52)] public int RoomId;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public ushort Flags;
        [FieldOffset(62)] public ushort Reserved0;
    }

    /// <summary>Player exited a habitat/base volume. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PlayerBaseExitSignal : ISignal
    {
        public const ushort DirectPlayerOutsideFlag = 1 << 0;
        public const ushort SanitizedBaseCenterFlag = 1 << 15;

        [FieldOffset(0)] public AbsoluteUniversePosition BaseCenterAup;
        [FieldOffset(48)] public int BaseId;
        [FieldOffset(52)] public int RoomId;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public ushort Flags;
        [FieldOffset(62)] public ushort Reserved0;
    }

    /// <summary>Weather transition lane payload. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct WeatherChangedSignal : ISignal
    {
        [FieldOffset(0)] public float Strength01;
        [FieldOffset(4)] public float FlowFieldScale;
        [FieldOffset(8)] public uint PreviousWeatherHash;
        [FieldOffset(12)] public uint WeatherHash;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte QualityWeightByte;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(22)] private ushort _padTail0;
        [FieldOffset(24)] private ulong _padTail1;
    }

    /// <summary>System pause lane payload. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SystemPauseSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public byte Paused;
        [FieldOffset(13)] public byte Flags;
        [FieldOffset(16)] public float RestoreScalar;
        [FieldOffset(20)] private uint _padTail0;
        [FieldOffset(24)] private ulong _padTail1;
    }

    /// <summary>Global simulation-bucket presentation sync lane. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SimulationBucketSyncSignal : ISignal
    {
        [FieldOffset(0)] public float InterpolationAlpha;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public int ActiveSlowBucket;
        [FieldOffset(12)] public int SlowBucketMask;
        [FieldOffset(16)] public uint RebalanceSequence;
        [FieldOffset(20)] public byte ActiveSlowBucketCount;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(22)] private ushort _padTail0;
        [FieldOffset(24)] private ulong _padTail1;
    }

    /// <summary>Frame-pacing warning lane emitted by the master modulo orchestrator. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FramePacingWarningSignal : ISignal
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint SourceHash;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public float CurrentFrameMs;
        [FieldOffset(16)] public float TargetFrameMs;
        [FieldOffset(20)] public float PreSimulationMs;
        [FieldOffset(24)] public float ActiveBucketLoadMs;
        [FieldOffset(28)] public float JitterVarianceMs;
        [FieldOffset(32)] public float ExpectedMaxBucketLoadMs;
        [FieldOffset(36)] public float ExpectedMeanBucketLoadMs;
        [FieldOffset(40)] public int ActiveSlowBucket;
        [FieldOffset(44)] public int SlowBucketMask;
        [FieldOffset(48)] public uint RebalanceSequence;
        [FieldOffset(52)] public byte Severity;
        [FieldOffset(53)] private byte _padTail0;
        [FieldOffset(54)] private ushort _padTail1;
        [FieldOffset(56)] private ulong _padTail2;
    }

    /// <summary>Applies a committed AUP shift to runtime-space combat signal coordinates.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct CombatDamageSignalAupShiftTransformer : ISignalSnapshotTransformer<CombatDamageSignal>
    {
        [FieldOffset(0)]
        private float3 _shiftMeters;

        [FieldOffset(12)]
        private uint _pad0;

        public void SetShift(float3 shiftMeters)
        {
            _shiftMeters = shiftMeters;
            _pad0 = 0u;
        }

        public void Transform(ref CombatDamageSignal signal)
        {
            // CombatDamageSignal stores absolute AUP; origin shifts do not mutate the fact.
        }
    }
}
