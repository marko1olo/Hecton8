using System.Runtime.InteropServices;
using Hecton8.Core.Memory.Layout;
using Unity.Mathematics;
using AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>Manual cockpit override latch packet emitted by physical VR lever controls. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ManualOverridePulledSignal : ISignal
    {
        public const byte FlagVrGrip = 1 << 0;
        public const byte FlagNonVrFallback = 1 << 1;
        public const byte FlagLatched = 1 << 2;
        public const byte HandUnknown = 0;
        public const byte HandLeft = 1;
        public const byte HandRight = 2;

        [FieldOffset(0)] public float3 LeverLocalPosition;
        [FieldOffset(12)] public float AngleDegrees;
        [FieldOffset(16)] public float GripStrength01;
        [FieldOffset(20)] public uint SourceHash;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public ushort Sequence;
        [FieldOffset(30)] public byte Flags;
        [FieldOffset(31)] public byte HandSide;
        [FieldOffset(32)] public float3 PivotLocalPosition;
        [FieldOffset(44)] public float VelocityDegreesPerSecond;
        [FieldOffset(48)] private ulong _padTail0;
        [FieldOffset(56)] private ulong _padTail1;
    }

    /// <summary>Hash-only HUD notification signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HUDNotificationSignal : ISignal
    {
        [FieldOffset(0)] public uint MessageHash;
        [FieldOffset(4)] public uint ContextHash;
        [FieldOffset(8)] public uint SourceId;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] private ushort _padTail0;
        [FieldOffset(20)] private uint _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>Diegetic physical-HUD prompt signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DiegeticHudSignal : ISignal
    {
        public const byte PromptManualRelease = 1;
        public const byte FlagPersistent = 1 << 0;

        [FieldOffset(0)] public uint MessageHash;
        [FieldOffset(4)] public uint ContextHash;
        [FieldOffset(8)] public uint SourceHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte PromptKind;
        [FieldOffset(17)] public byte Priority;
        [FieldOffset(18)] public byte Flags;
        [FieldOffset(19)] private byte _padTail0;
        [FieldOffset(20)] private uint _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>Scan-log dirty-state signal for PDA, crafting, and barter consumers. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ScanLogChangedSignal : ISignal
    {
        public const byte ReasonLoaded = 1;
        public const byte ReasonEntryAdded = 2;
        public const byte ReasonRecentChanged = 3;

        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint EntryHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public ushort EntryCount;
        [FieldOffset(14)] public ushort RecentCount;
        [FieldOffset(16)] public byte Reason;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(20)] public uint Revision;
        [FieldOffset(24)] public uint CategoryHash;
        [FieldOffset(31)] private byte _pad;
    }

    /// <summary>PDA exchange dirty-state signal for barter UI and relay consumers. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PdaExchangeStateChangedSignal : ISignal
    {
        public const byte ReasonExecuted = 1;
        public const byte ReasonLoaded = 2;
        public const byte ReasonInventoryChanged = 3;
        public const byte ReasonScanLogChanged = 4;
        public const byte FlagInventoryDirty = 1 << 0;
        public const byte FlagScanLogDirty = 1 << 1;

        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public int OfferCount;
        [FieldOffset(12)] public int RecentTransactionCount;
        [FieldOffset(16)] public int ExecutionStateCount;
        [FieldOffset(20)] public byte Reason;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(31)] private byte _pad;
    }

    /// <summary>Vehicle upgrade bitmask mutation signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VehicleUpgradesChangedSignal : ISignal
    {
        public const byte ReasonPenalty = 1;
        public const byte ReasonInstall = 2;

        [FieldOffset(0)] public uint SourceId;
        [FieldOffset(4)] public uint UpgradeMask;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public float SafeDepthBonusMeters;
        [FieldOffset(16)] public float PermanentSafeDepthPenaltyMeters;
        [FieldOffset(20)] public byte Reason;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(31)] private byte _pad;
    }

    /// <summary>Cached platform thermal state transition signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ThermalStateChangedSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public byte Severity;
        [FieldOffset(13)] public byte PreviousSeverity;
        [FieldOffset(14)] public byte ThermalStatus;
        [FieldOffset(15)] public byte Flags;
        [FieldOffset(16)] public short TemperatureTenthsCelsius;
        [FieldOffset(18)] public byte BatteryPercent;
        [FieldOffset(20)] public uint ActionMask;
        [FieldOffset(24)] private ulong _padTail0;
    }

    /// <summary>Cached platform battery level signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BatteryLevelSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public byte BatteryPercent;
        [FieldOffset(13)] public byte BatteryStatus;
        [FieldOffset(14)] public byte Flags;
        [FieldOffset(16)] public uint ActionMask;
        [FieldOffset(20)] private uint _padTail0;
        [FieldOffset(24)] private ulong _padTail1;
    }

    /// <summary>Recon data signal for PDA map population. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ReconDataSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)] public uint EntryHash;
        [FieldOffset(52)] public uint SourceId;
        [FieldOffset(56)] public byte ReconKind;
        [FieldOffset(57)] public byte Flags;
        [FieldOffset(58)] private ushort _padTail0;
        [FieldOffset(60)] private uint _padTail1;
    }

    /// <summary>Save start/end gate signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SaveLifecycleSignal : ISignal
    {
        public const byte FailureFlag = 1 << 0;
        public const byte SaveOperationFlag = 1 << 1;
        public const byte LoadOperationFlag = 1 << 2;

        [FieldOffset(0)] public uint SlotHash;
        [FieldOffset(4)] public uint OperationId;
        [FieldOffset(8)] public float Progress01;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte State;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] private ushort _padTail0;
        [FieldOffset(20)] private uint _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>Macro database sector hydration completion lane. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MacroDatabaseSectorHydrationSignal : ISignal
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public long FileOffset;
        [FieldOffset(16)] public int PayloadBytes;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public byte SourceTier;
        [FieldOffset(25)] public byte Flags;
        [FieldOffset(26)] private ushort _padTail0;
        [FieldOffset(28)] private uint _padTail1;
    }

    /// <summary>WFC outpost generation completion lane. GridHandle resolves native packed cell data. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct WfcOutpostGeneratedSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition OriginAup;
        [FieldOffset(48)] public ulong SectorHash;
        [FieldOffset(56)] public uint GridHandle;
        [FieldOffset(60)] public uint GenerationSequence;
        [FieldOffset(64)] public int3 Dimensions;
        [FieldOffset(76)] public float CellSizeMeters;
        [FieldOffset(80)] public float FloorHeightMeters;
        [FieldOffset(84)] public uint GridHash;
        [FieldOffset(88)] public uint Frame;
        [FieldOffset(92)] public ushort CellCount;
        [FieldOffset(94)] public ushort Flags;
        [FieldOffset(96)] private ulong _padTail0;
        [FieldOffset(104)] private ulong _padTail1;
        [FieldOffset(112)] private ulong _padTail2;
        [FieldOffset(120)] private ulong _padTail3;
    }

    /// <summary>WFC outpost mutable-cell state change lane. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct WfcOutpostStateChangedSignal : ISignal
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public ushort CellIndex;
        [FieldOffset(10)] public byte PreviousFlags;
        [FieldOffset(11)] public byte CurrentFlags;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public uint SourceHash;
        [FieldOffset(20)] public byte Flags;
        [FieldOffset(21)] private byte _padTail0;
        [FieldOffset(22)] private ushort _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    /// <summary>WFC outpost door power state lane. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct WfcOutpostDoorPowerSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition DoorAup;
        [FieldOffset(48)] public ulong SectorHash;
        [FieldOffset(56)] public uint GridHandle;
        [FieldOffset(60)] public uint NodeId;
        [FieldOffset(64)] public ushort CellIndex;
        [FieldOffset(66)] public ushort DoorId;
        [FieldOffset(68)] public float Voltage;
        [FieldOffset(72)] public uint Frame;
        [FieldOffset(76)] public byte Unlocked;
        [FieldOffset(77)] public byte Flags;
        [FieldOffset(78)] public ushort Reserved0;
        [FieldOffset(80)] public ulong Reserved1;
        [FieldOffset(88)] public ulong Reserved2;
        [FieldOffset(96)] public ulong Reserved3;
        [FieldOffset(104)] public ulong Reserved4;
        [FieldOffset(112)] public ulong Reserved5;
        [FieldOffset(120)] public ulong Reserved6;
    }

    /// <summary>Save metadata screenshot completion payload. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SaveMetadataReadySignal : ISignal
    {
        public const byte Completed = 1;
        public const byte DeferredByQuality = 2;
        public const byte Failed = 3;
        public const byte TimedOut = 4;
        public const byte ReusedExisting = 5;

        public const byte QualityDeferredFlag = 1 << 0;
        public const byte FailureFlag = 1 << 1;
        public const byte ReusedExistingFlag = 1 << 2;

        [FieldOffset(0)] public uint SlotHash;
        [FieldOffset(4)] public uint OperationId;
        [FieldOffset(8)] public uint ScreenshotBytes;
        [FieldOffset(12)] public uint ScreenshotHash;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte Result;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(22)] private ushort _padTail0;
        [FieldOffset(24)] private ulong _padTail1;
    }

    /// <summary>Compliance violation signal. Size: 32 bytes.</summary>
    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ComplianceViolationSignal : ISignal
    {
        [FieldOffset(0)] public uint RuleHash;
        [FieldOffset(4)] public uint SystemHash;
        [FieldOffset(8)] public uint ContextHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte Severity;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] private ushort _padTail0;
        [FieldOffset(20)] private uint _padTail1;
        [FieldOffset(24)] private ulong _padTail2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct EncyclopediaUnlockSignal : ISignal
    {
        [FieldOffset(0)] public uint EntityHash;
        [FieldOffset(4)] public uint SourceHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint ScanId;
        [FieldOffset(16)] public byte Kind;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] public ushort RequiredToolLevel;
        [FieldOffset(20)] public uint Reserved0;
        [FieldOffset(24)] public ulong Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct EntityDepletedSignal : ISignal
    {
        [FieldOffset(0)] public uint EntityHash;
        [FieldOffset(4)] public uint SourceHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public ushort WordIndex;
        [FieldOffset(14)] public byte Operation;
        [FieldOffset(15)] public byte Flags;
        [FieldOffset(16)] public long SectorHash;
        [FieldOffset(24)] public ulong DepletionMask;
    }
}
