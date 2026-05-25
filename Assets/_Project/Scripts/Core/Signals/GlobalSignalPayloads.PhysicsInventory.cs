using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>Physics-to-sound impact signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ImpactSignal : ISignal
    {
        public const int ExpectedCapacity = 256;
        public const int MaxFrameSignals = 256;
        public const int LowTierFrameSignals = 64;
        public const uint LaneHash = 1490821407u; // FNV32("ImpactSignal")

        [FieldOffset(0)] public AbsoluteUniversePosition PointAup;
        [FieldOffset(48)] public float Force;
        [FieldOffset(48)] public float Velocity;
        [FieldOffset(52)] public float Intensity;
        [FieldOffset(52)] public float Mass;
        [FieldOffset(56)] public uint PrimaryBodyId;
        [FieldOffset(56)] public uint MaterialHash;
        [FieldOffset(60)] public byte WeightClass;
        [FieldOffset(61)] public byte PrimaryMaterialId;
        [FieldOffset(62)] public byte SecondaryMaterialId;
        [FieldOffset(63)] public byte Flags;
    }

    /// <summary>Kinematic CCD impact packet with exact AUP hit point and slide normal. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct HighSpeedImpactSignal : ISignal
    {
        public const int ExpectedCapacity = 128;
        public const int MaxFrameSignals = 128;
        public const int LowTierFrameSignals = 32;
        public const uint LaneHash = 2004661978u; // FNV32("HighSpeedImpactSignal")
        public const byte SourcePlayer = 1;
        public const byte SourceVehicle = 2;
        public const byte SourceLeviathan = 3;
        public const byte FlagCornerHalt = 1 << 0;
        public const byte FlagLowTierStop = 1 << 1;
        public const byte MaterialOrganic = 0;
        public const byte MaterialMetal = 1;
        public const byte MaterialGlass = 2;

        [FieldOffset(0)] public AbsoluteUniversePosition PointAup;
        [FieldOffset(48)] public float3 Normal;
        [FieldOffset(60)] public float LostKineticEnergy;
        [FieldOffset(60)] public float KineticEnergy;
        [FieldOffset(64)] public float ImpactSpeed;
        [FieldOffset(68)] public uint SourceHash;
        [FieldOffset(72)] public uint TargetHash;
        [FieldOffset(76)] public uint Frame;
        [FieldOffset(80)] public byte SourceKind;
        [FieldOffset(81)] public byte Flags;
        [FieldOffset(82)] public byte PrimaryMaterialId;
        [FieldOffset(83)] public byte SecondaryMaterialId;
        [FieldOffset(84)] public float EffectiveMass;
        [FieldOffset(88)] public uint MaterialHash;
        [FieldOffset(92)] public uint Reserved0;
        [FieldOffset(96)] public ulong Reserved1;
        [FieldOffset(104)] public ulong Reserved2;
        [FieldOffset(112)] public ulong Reserved3;
        [FieldOffset(120)] public ulong Reserved4;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComposeMaterialHash(uint targetHash, byte primaryMaterialId, byte secondaryMaterialId)
        {
            uint hash = 2166136261u;
            hash = (hash ^ targetHash) * 16777619u;
            hash = (hash ^ primaryMaterialId) * 16777619u;
            hash = (hash ^ secondaryMaterialId) * 16777619u;
            return hash != 0u ? hash : 1u;
        }
    }

    /// <summary>Haptic request packet sourced from high-energy physical impacts. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HapticRequest : ISignal
    {
        public const byte ChannelCollision = 1;
        public const byte ChannelLightThud = 2;
        public const byte ChannelGearScrape = 3;
        public const byte ChannelVehicleCritical = 4;
        public const byte ChannelCrush = 5;
        public const byte ChannelMicroVibration = 6;
        public const byte FlagLightThud = 1 << 0;
        public const byte FlagCrush = 1 << 1;
        public const byte FlagMicroVibration = 1 << 2;

        [FieldOffset(0)] public float Intensity01;
        [FieldOffset(4)] public float DurationSeconds;
        [FieldOffset(8)] public float Frequency01;
        [FieldOffset(12)] public uint SourceHash;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte Channel;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(22)] private ushort _padTail0;
        [FieldOffset(24)] private uint _padTail1;
        [FieldOffset(28)] private uint _padTail2;
    }

    /// <summary>Player state adapter lane for animation, traversal, and contextual physiology. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PlayerStateSignal : ISignal
    {
        public const byte StateNone = 0;
        public const byte StateSqueezing = 1;
        public const byte StateClimbing = 2;
        public const byte FlagActive = 1 << 0;
        public const byte FlagSqueezing = FlagActive;
        public const byte FlagSdfGradientValid = 1 << 1;
        public const byte FlagLowTierGradient = 1 << 2;
        public const byte FlagAupShiftSafe = 1 << 3;
        public const byte FlagClimbing = 1 << 4;
        public const byte FlagVrGrip = 1 << 5;
        public const byte FlagLadderSlip = 1 << 6;
        public const byte FlagLowTierCameraSlide = 1 << 7;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float Intensity01;
        [FieldOffset(52)] public uint SourceHash;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public byte State;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] public ushort Reserved0;
    }

    public static class SurvivalVitalsChangedSignalFlags
    {
        public const uint Oxygen = 1u << 0;
        public const uint Energy = 1u << 1;
        public const uint Integrity = 1u << 2;
        public const uint Depth = 1u << 3;
        public const uint Temperature = 1u << 4;
        public const uint Thermal = 1u << 5;
        public const uint Injury = 1u << 6;
        public const uint Death = 1u << 7;
        public const uint OxygenCritical = 1u << 8;
        public const uint Pressure = 1u << 9;
    }

    /// <summary>Player survival-vitals dirty mask for UI and advisory consumers. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SurvivalVitalsChangedSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float Oxygen01;
        [FieldOffset(20)] public float Energy01;
        [FieldOffset(24)] public float Integrity01;
        [FieldOffset(28)] public byte DeathCause;
        [FieldOffset(29)] private byte _padTail0;
        [FieldOffset(30)] private ushort _padTail1;
    }

    /// <summary>Player delayed-action progress lane for UI and feedback consumers. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PlayerActionProgressSignal : ISignal
    {
        public const byte ActionKindGeneric = 0;
        public const byte ActionKindMedical = 1;
        public const byte ActionKindOxygen = 2;
        public const byte ActionKindFood = 3;
        public const byte FlagHasItem = 1 << 0;

        [FieldOffset(0)] public float Progress01;
        [FieldOffset(4)] public uint ItemHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public ushort ActiveToolSlot;
        [FieldOffset(14)] public byte ActionKind;
        [FieldOffset(15)] public byte Flags;
        [FieldOffset(31)] private byte _pad;
    }

    /// <summary>Player delayed-action completion lane. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PlayerActionCompletedSignal : ISignal
    {
        public const byte FlagHasItem = 1 << 0;
        public const byte FlagInventoryAnchorValid = 1 << 1;

        [FieldOffset(0)] public uint ItemHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public ushort InventoryAnchorX;
        [FieldOffset(10)] public ushort InventoryAnchorY;
        [FieldOffset(12)] public byte ActionKind;
        [FieldOffset(13)] public byte Flags;
        [FieldOffset(31)] private byte _pad;
    }

    /// <summary>Player delayed-action cancellation lane. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PlayerActionCancelledSignal : ISignal
    {
        public const byte ReasonGeneric = 0;
        public const byte FlagHasItem = 1 << 0;

        [FieldOffset(0)] public uint ItemHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public float Progress01;
        [FieldOffset(12)] public byte ActionKind;
        [FieldOffset(13)] public byte Reason;
        [FieldOffset(14)] public byte Flags;
        [FieldOffset(31)] private byte _pad;
    }

    public static class InventoryCommandSignalCommands
    {
        public const byte Sort = 1;
        public const byte DropNonEquippedResources = 2;
    }

    public static class InventoryCommandSignalPayloadFlags
    {
        public const ushort VaultPenaltyRules = 1 << 0;
        public const ushort FallbackWhenRuleTableMissing = 1 << 1;
        public const ushort RespawnDeathAupSideband = 1 << 2;
    }

    /// <summary>Inventory command lane payload. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventoryCommandSignal : ISignal
    {
        [FieldOffset(0)] public uint InventoryHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public byte Command;
        [FieldOffset(13)] public byte Flags;
        [FieldOffset(14)] public ushort PayloadFlags;
        [FieldOffset(16)] public uint Payload0;
        [FieldOffset(20)] public uint Payload1;
        [FieldOffset(24)] public uint Payload2;
        [FieldOffset(28)] public uint Payload3;
    }

    /// <summary>Inventory-owned respawn death AUP sideband. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct InventoryRespawnDeathAupSignal : ISignal
    {
        public const uint LaneHash = 0x49524441u; // IRDA
        public const int ExpectedCapacity = 16;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 16;

        [FieldOffset(0)] public double3 DeathAUP;
        [FieldOffset(24)] public uint InventoryHash;
        [FieldOffset(28)] public uint Frame;
        [FieldOffset(32)] public uint Sequence;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public uint SourceHash;
        [FieldOffset(44)] private uint _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
    }

    /// <summary>Inventory-owned death loot cache enqueue. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct InventoryDeathLootCacheSignal : ISignal
    {
        public const uint LaneHash = 0x49444C43u; // IDLC
        public const int ExpectedCapacity = 64;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 64;

        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public ulong GeneticsMask;
        [FieldOffset(56)] public uint InventoryHash;
        [FieldOffset(60)] public uint ItemHash;
        [FieldOffset(64)] public uint Sequence;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public ushort Quantity;
        [FieldOffset(74)] public ushort QualityMilli;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] public ushort StateFlags;
        [FieldOffset(82)] private ushort _pad0;
        [FieldOffset(84)] private uint _pad1;
        [FieldOffset(88)] private ulong _pad2;
        [FieldOffset(96)] private ulong _pad3;
        [FieldOffset(104)] private ulong _pad4;
        [FieldOffset(112)] private ulong _pad5;
        [FieldOffset(120)] private ulong _pad6;
    }

    /// <summary>Inventory-owned respawn penalty result. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventoryRespawnPenaltyResultSignal : ISignal
    {
        public const uint LaneHash = 0x49525052u; // IRPR
        public const int ExpectedCapacity = 16;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 16;

        [FieldOffset(0)] public uint InventoryHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public uint DroppedCount;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] private uint _pad0;
        [FieldOffset(24)] private ulong _pad1;
    }

    /// <summary>Inventory mutation lane payload. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventoryChangedSignal : ISignal
    {
        [FieldOffset(0)] public uint InventoryHash;
        [FieldOffset(4)] public uint Revision;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public ushort OccupiedCells;
        [FieldOffset(14)] public byte Flags;
        [FieldOffset(16)] public float TotalMassKg;
        [FieldOffset(20)] public float CarryCapacityKg;
        [FieldOffset(24)] public float Load01;
        [FieldOffset(28)] private uint _padTail0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ItemDurabilityChangedSignal : ISignal
    {
        public const byte ReasonCorrosion = 1;
        public const byte ReasonRepair = 2;
        public const byte ReasonBreak = 3;
        public const byte FlagBroken = 1 << 0;

        [FieldOffset(0)] public uint InventoryHash;
        [FieldOffset(4)] public uint ItemHash;
        [FieldOffset(8)] public float Durability01;
        [FieldOffset(12)] public float AverageEquippedDurability01;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public ushort SlotIndex;
        [FieldOffset(22)] public byte Reason;
        [FieldOffset(23)] public byte Flags;
        [FieldOffset(24)] public uint BiomeHash;
        [FieldOffset(28)] private uint _padTail0;
    }

    /// <summary>First-party item lifecycle lane. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ItemLifecycleSignal : ISignal
    {
        public const byte ActionCollected = 1;
        public const byte ActionRecycled = 2;
        public const byte ActionDiscarded = 3;
        public const byte FlagHasRuntimePosition = 1 << 0;
        public const byte FlagRawResource = 1 << 1;
        public const byte FlagMaterialCategory = 1 << 2;
        public const byte FlagPlasticLike = 1 << 3;

        [FieldOffset(0)] public float3 RuntimePosition;
        [FieldOffset(12)] public float UnitWeightKg;
        [FieldOffset(16)] public uint ItemHash;
        [FieldOffset(20)] public uint InteractorHash;
        [FieldOffset(24)] public int Quantity;
        [FieldOffset(28)] public int YieldUnitCount;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public uint Sequence;
        [FieldOffset(40)] public uint PollutionMilli;
        [FieldOffset(44)] public byte Action;
        [FieldOffset(45)] public byte Category;
        [FieldOffset(46)] public byte ResourceFamily;
        [FieldOffset(47)] public byte Flags;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    /// <summary>Resource-to-inventory yield signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ItemAcquiredSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint ItemHash;
        [FieldOffset(52)] public uint OreHash;
        [FieldOffset(56)] public ushort Quantity;
        [FieldOffset(58)] public byte SourceKind;
        [FieldOffset(59)] public byte Flags;
        [FieldOffset(60)] public uint Frame;
    }

    /// <summary>Radiation grid/physiology dose signal. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RadiationDoseSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public float Dose;
        [FieldOffset(52)] public float Intensity01;
        [FieldOffset(56)] public uint SourceId;
        [FieldOffset(60)] public byte DoseKind;
        [FieldOffset(61)] public byte Flags;
        [FieldOffset(62)] private ushort _padTail0;
    }
}
