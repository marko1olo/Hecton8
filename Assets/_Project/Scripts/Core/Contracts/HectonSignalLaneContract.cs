using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Acoustic-space absolute universe position used by audio propagation and voice virtualization. Size: 40 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    public struct AcousticAup
    {
        public const int CellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersInt;

        [FieldOffset(0)] public long GridX;
        [FieldOffset(8)] public long GridY;
        [FieldOffset(16)] public long GridZ;
        [FieldOffset(24)] public float3 Local;
        [FieldOffset(36)] private byte _pad0;
        [FieldOffset(37)] private byte _pad1;
        [FieldOffset(38)] private byte _pad2;
        [FieldOffset(39)] private byte _pad3;

        public AcousticAup(long gridX, long gridY, long gridZ, float3 local)
        {
            GridX = gridX;
            GridY = gridY;
            GridZ = gridZ;
            Local = local;
            _pad0 = 0;
            _pad1 = 0;
            _pad2 = 0;
            _pad3 = 0;
        }

        public static float3 RelativeFloat3(in AcousticAup position, in AcousticAup origin)
        {
            double cellSize = CellSizeMeters;
            double gridDeltaX = (double)position.GridX - origin.GridX;
            double gridDeltaY = (double)position.GridY - origin.GridY;
            double gridDeltaZ = (double)position.GridZ - origin.GridZ;
            double x = (gridDeltaX * cellSize) + (double)position.Local.x - origin.Local.x;
            double y = (gridDeltaY * cellSize) + (double)position.Local.y - origin.Local.y;
            double z = (gridDeltaZ * cellSize) + (double)position.Local.z - origin.Local.z;
            return new float3(
                ClampRelativeComponentToFloat(x),
                ClampRelativeComponentToFloat(y),
                ClampRelativeComponentToFloat(z));
        }

        public static float DistanceMeters(in AcousticAup a, in AcousticAup b)
        {
            double cellSize = CellSizeMeters;
            double gridDeltaX = (double)a.GridX - b.GridX;
            double gridDeltaY = (double)a.GridY - b.GridY;
            double gridDeltaZ = (double)a.GridZ - b.GridZ;
            double x = ClampDistanceComponent((gridDeltaX * cellSize) + (double)a.Local.x - b.Local.x);
            double y = ClampDistanceComponent((gridDeltaY * cellSize) + (double)a.Local.y - b.Local.y);
            double z = ClampDistanceComponent((gridDeltaZ * cellSize) + (double)a.Local.z - b.Local.z);
            double distanceSq = x * x + y * y + z * z;
            if (distanceSq <= 0.0 || !math.isfinite(distanceSq))
                return 0f;

            return (float)math.min(HectonPhysicsContract.AupMaxDistanceReturnMeters, math.sqrt(distanceSq));
        }

        private static float ClampRelativeComponentToFloat(double value)
        {
            const double maxFloatSafe = HectonPhysicsContract.AupMaxFloatSafeMeters;
            if (double.IsNaN(value))
                return 0f;
            if (!math.isfinite(value))
                return value < 0.0 ? (float)-maxFloatSafe : (float)maxFloatSafe;
            return (float)math.clamp(value, -maxFloatSafe, maxFloatSafe);
        }

        private static double ClampDistanceComponent(double value)
        {
            const double maxDistance = HectonPhysicsContract.AupMaxDistanceReturnMeters;
            if (double.IsNaN(value))
                return maxDistance;
            if (!math.isfinite(value))
                return value < 0.0 ? -maxDistance : maxDistance;
            return math.clamp(value, -maxDistance, maxDistance);
        }

        public static bool IsFinite(in AcousticAup aup)
        {
            return math.all(math.isfinite(aup.Local));
        }
    }

    /// <summary>
    /// DSP echo tap payload produced by acoustic systems and consumed by sensory systems. Size: 144 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 144)]
    public struct AcousticEchoTap
    {
        [FieldOffset(0)] public AcousticAup SourceAup;
        [FieldOffset(40)] public AcousticAup ListenerAup;
        [FieldOffset(80)] public float3 Position;
        [FieldOffset(92)] public float Magnitude;
        [FieldOffset(96)] public float Volume01;
        [FieldOffset(100)] public float DelaySeconds;
        [FieldOffset(104)] public float LowPassCutoffHz;
        [FieldOffset(108)] public float Rt60Seconds;
        [FieldOffset(112)] public uint SoundHash;
        [FieldOffset(116)] public uint SourceId;
        [FieldOffset(120)] public uint ClipHash;
        [FieldOffset(124)] public byte Flags;
        [FieldOffset(125)] public byte QualityTier;
        [FieldOffset(126)] private byte _pad0;
        [FieldOffset(127)] private byte _pad1;
        [FieldOffset(128)] private byte _pad2;
        [FieldOffset(129)] private byte _pad3;
        [FieldOffset(130)] private byte _pad4;
        [FieldOffset(131)] private byte _pad5;
        [FieldOffset(132)] private byte _pad6;
        [FieldOffset(133)] private byte _pad7;
        [FieldOffset(134)] private byte _pad8;
        [FieldOffset(135)] private byte _pad9;
        [FieldOffset(136)] private byte _pad10;
        [FieldOffset(137)] private byte _pad11;
        [FieldOffset(138)] private byte _pad12;
        [FieldOffset(139)] private byte _pad13;
        [FieldOffset(140)] private byte _pad14;
        [FieldOffset(141)] private byte _pad15;
        [FieldOffset(142)] private byte _pad16;
        [FieldOffset(143)] private byte _pad17;
    }
}

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Marker for unmanaged signal-lane payloads. Implemented only by blittable structs.
    /// </summary>
    [Preserve]
    public interface ISignal
    {
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FrameTimeSignal : ISignal
    {
        public const int ExpectedCapacity = 32;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 0x46544D53u; // FTMS

        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public float CurrentFrameTimeMs;
        [FieldOffset(8)] public float FrameTimeEwmaMs;
        [FieldOffset(12)] public float TargetFrameTimeMs;
        [FieldOffset(16)] public float JitterSigmaMs;
        [FieldOffset(20)] public byte PressureLevel;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(22)] public ushort Reserved;
        [FieldOffset(24)] public uint Sequence;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct KillSwitchSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 32;
        public const int LowTierFrameSignals = 8;
        public const uint LaneHash = 0x4B534857u; // KSHW

        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] private uint _pad0;
        [FieldOffset(8)] public ulong PreviousMask;
        [FieldOffset(16)] public ulong CurrentMask;
        [FieldOffset(24)] public float SystemHealthIndex01;
        [FieldOffset(28)] public byte PreviousLevel;
        [FieldOffset(29)] public byte CurrentLevel;
        [FieldOffset(30)] public ushort Flags;
    }

    /// <summary>Registry-owned emergency kill-switch bit delta. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SystemKillSwitchBitsSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 32;
        public const int LowTierFrameSignals = 8;
        public const uint LaneHash = HectonSignalLaneContract.SystemKillSwitchBitsSignalStableHash;
        public const byte FlagEnabled = 1 << 0;
        public const byte FlagRegistryOwner = 1 << 1;

        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint SourceHash;
        [FieldOffset(8)] public uint PreviousMask;
        [FieldOffset(12)] public uint CurrentMask;
        [FieldOffset(16)] public uint ChangedMask;
        [FieldOffset(20)] public uint EnabledMask;
        [FieldOffset(24)] public byte Flags;
        [FieldOffset(25)] private byte _pad0;
        [FieldOffset(26)] private ushort _pad1;
        [FieldOffset(28)] private uint _pad2;
    }

    /// <summary>Clustered habitat structural warning lane for localized audio, visor, panic, and power-light cues. Size: 64 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BaseStructuralWarningSignal : ISignal
    {
        public const uint LaneHash = 0x42535744u; // BSWD
        public const uint FlagRedAlert = 1u << 0;
        public const uint FlagNonFinite = 1u << 1;
        public const uint FlagThrottled = 1u << 2;
        public const uint FlagHypoxiaPanicCandidate = 1u << 3;

        [FieldOffset(0)] public Hecton8.Core.Contracts.AcousticAup EpicenterAup;
        [FieldOffset(40)] public uint BaseHash;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] public float HighestStress01;
        [FieldOffset(52)] public float AudioIntensity01;
        [FieldOffset(56)] public float PanicScalar01;
        [FieldOffset(60)] public uint CriticalFlags;
    }

    /// <summary>
    /// Last flushed state for one typed signal lane.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SignalLaneTelemetry
    {
        // Low32: pushed last flush. High32: corrupted total.
        [FieldOffset(0)] public ulong Reserved2;
        [FieldOffset(8)] public uint LaneHash;
        [FieldOffset(12)] public int QueuedBeforeFlush;
        [FieldOffset(16)] public int SnapshotCount;
        [FieldOffset(20)] public int DroppedCount;
        [FieldOffset(24)] public int CoalescedCount;
        // Low byte: layout policy flags. High byte: legacy MPSC writer opens last flush, saturated to 255.
        [FieldOffset(28)] public ushort Reserved1;
        // Bits: 0 storm, 1 non-critical VFX, 2 fatal, 3 coalesced, 4 corrupt, 5 cache-line stride debt, 6 legacy MPSC writer opened.
        [FieldOffset(30)] public byte Flags;
        // Payload stride bytes, saturated to 255.
        [FieldOffset(31)] public byte Reserved0;
    }

    /// <summary>Procedural instance culling overload signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct CullingOverloadSignal : ISignal
    {
        [FieldOffset(0)] public int VisibleInstances;
        [FieldOffset(4)] public int CulledInstances;
        [FieldOffset(8)] public int SourceInstances;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public float CullDistanceMeters;
        [FieldOffset(20)] public float VramUsedMb;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint SourceHash;
    }

    /// <summary>Owner-local async persistence request packet. Not a SignalBus lane. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SaveRequestSignal
    {
        public const byte ManualSlotFlag = 1 << 0;

        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint OperationId;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public byte SlotIndex;
        [FieldOffset(13)] public byte Flags;
        [FieldOffset(14)] private ushort _pad0;
        [FieldOffset(16)] private uint _pad1;
        [FieldOffset(20)] private uint _pad2;
        [FieldOffset(24)] private ulong _pad3;
    }

    /// <summary>Async persistence completion lane payload. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SaveCompletedSignal : ISignal
    {
        [FieldOffset(0)] public uint SlotHash;
        [FieldOffset(4)] public uint OperationId;
        [FieldOffset(8)] public uint DurationMilliseconds;
        [FieldOffset(12)] public uint CompressedSizeBytes;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public byte Result;
        [FieldOffset(21)] public byte Flags;
        [FieldOffset(22)] private ushort _pad0;
        [FieldOffset(24)] private uint _pad1;
        [FieldOffset(28)] private uint _pad2;
    }

    /// <summary>Async persistence status lane payload for diegetic save indicators. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SaveStatusSignal : ISignal
    {
        public const byte Queued = 0;
        public const byte InProgress = 1;
        public const byte Completed = 2;
        public const byte Failed = 3;
        public const byte Rejected = 4;

        [FieldOffset(0)] public uint SlotHash;
        [FieldOffset(4)] public uint OperationId;
        [FieldOffset(8)] public float Progress01;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public byte State;
        [FieldOffset(17)] public byte Flags;
        [FieldOffset(18)] private ushort _pad0;
        [FieldOffset(20)] private uint _pad1;
        [FieldOffset(24)] private ulong _pad2;
    }

    /// <summary>Global time sync signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct GlobalTimeSyncSignal : ISignal
    {
        [FieldOffset(0)] public double WorldSeconds;
        [FieldOffset(8)] public float TimeScale;
        [FieldOffset(12)] public float MoonPhase01;
        [FieldOffset(16)] public uint Sequence;
        [FieldOffset(20)] public byte Flags;
        [FieldOffset(21)] private byte _pad0;
        [FieldOffset(22)] private ushort _pad1;
        [FieldOffset(24)] private ulong _pad2;
    }

    /// <summary>Authoritative dispatcher time-dilation signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TimeDilationSignal : ISignal
    {
        [FieldOffset(0)] public float Scalar;
        [FieldOffset(4)] public float UnscaledDeltaTime;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public uint ReasonHash;
        [FieldOffset(20)] public byte Flags;
        [FieldOffset(21)] private byte _pad0;
        [FieldOffset(22)] private ushort _pad1;
        [FieldOffset(24)] private ulong _pad2;
    }

    /// <summary>Simulation pause request signal. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SimulationPauseSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public byte Paused;
        [FieldOffset(13)] public byte Flags;
        [FieldOffset(14)] private ushort _pad0;
        [FieldOffset(16)] public float RestoreScalar;
        [FieldOffset(20)] private uint _pad1;
        [FieldOffset(24)] private ulong _pad2;
    }

    /// <summary>Cheap bullet-time post-process control signal. Size: 32 bytes. QualityWeightBits stores math.asuint(0..1).</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BulletTimeVisualSignal : ISignal
    {
        [FieldOffset(0)] public float Intensity01;
        [FieldOffset(4)] public float Scalar;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Sequence;
        [FieldOffset(16)] public uint QualityWeightBits;
        [FieldOffset(20)] public byte Flags;
        [FieldOffset(21)] private byte _pad0;
        [FieldOffset(22)] private ushort _pad1;
        [FieldOffset(24)] private ulong _pad2;
    }
}

namespace Hecton8.Core.Contracts
{
    public static class HectonSignalLaneContract
    {
        public const byte AcousticPingSignal = 1;
        public const byte AnomalyProximitySignal = 2;
        public const byte AtmosphericReentrySignal = 3;
        public const byte AupPreShiftSignal = 4;
        public const byte AupShiftSignal = 5;
        public const byte BaseModuleCompromisedSignal = 6;
        public const byte BatteryLevelSignal = 7;
        public const byte BiomeChangedSignal = 8;
        public const byte BiomeGradientSignal = 9;
        public const byte BrownoutSignal = 10;
        public const byte BubbleSpawnSignal = 11;
        public const byte CameraFrustumSignal = 12;
        public const byte CameraPositionSignal = 13;
        public const byte ChunkDehydratedSignal = 14;
        public const byte CombatDamageSignal = 15;
        public const byte CompassCalibratedSignal = 16;
        public const byte CpuStarvationSignal = 17;
        public const byte CraftingCompletedSignal = 18;
        public const byte CullingOverloadSignal = 19;
        public const byte DebrisSpawnSignal = 20;
        public const byte DebugSignal = 21;
        public const byte DeferredSubmarineImpactSignal = 22;
        public const byte DesyncDetectedSignal = 23;
        public const byte DiegeticHudSignal = 24;
        public const byte DockingCompleteSignal = 25;
        public const byte DockingFailedSignal = 26;
        public const byte DockingRequestSignal = 27;
        public const byte DropPodLandedSignal = 28;
        public const byte EntityDeathSignal = 29;
        public const byte EntitySpawnSignal = 30;
        public const byte FaunaStateChangedSignal = 31;
        public const byte FluidImpulseSignal = 32;
        public const byte FramePacingWarningSignal = 33;
        public const byte FrameTimeSignal = 34;
        public const byte HapticRequest = 35;
        public const byte HighSpeedImpactSignal = 36;
        public const byte HullDeformedSignal = 37;
        public const byte HullRepairedSignal = 38;
        public const byte ImpactSignal = 39;
        public const byte InputSignal = 40;
        public const byte InputStateSignal = 41;
        public const byte InventoryChangedSignal = 42;
        public const byte InventoryCommandSignal = 43;
        public const byte ItemAcquiredSignal = 44;
        public const byte ItemDurabilityChangedSignal = 45;
        public const byte KccVelocitySignal = 46;
        public const byte KillSwitchSignal = 47;
        public const byte LaserCutterEventPayload = 48;
        public const byte LockstepSnapshotSignal = 49;
        public const byte LoreFragmentScannedSignal = 50;
        public const byte MacroDatabaseSectorHydrationSignal = 51;
        public const byte ManualOverridePulledSignal = 52;
        public const byte MemoryAddressShiftSignal = 53;
        public const byte MemoryPressureSignal = 54;
        public const byte MovementAcousticSignal = 55;
        public const byte PdaExchangeStateChangedSignal = 56;
        public const byte PhysicsEventPayload = 57;
        public const byte PhysiologyStateSignal = 58;
        public const byte PlayerActionCancelledSignal = 59;
        public const byte PlayerActionCompletedSignal = 60;
        public const byte PlayerActionProgressSignal = 61;
        public const byte PlayerBaseEnterSignal = 62;
        public const byte PlayerBaseExitSignal = 63;
        public const byte PlayerInputSignal = 64;
        public const byte PlayerLookTargetSignal = 65;
        public const byte PlayerStateSignal = 66;
        public const byte PlayerStressSignal = 67;
        public const byte PrologueCompleteSignal = 68;
        public const byte RadiationDoseSignal = 69;
        public const byte RadiationSourceSignal = 70;
        public const byte ReentryVfxStateSignal = 71;
        public const byte ResolutionChangedSignal = 72;
        public const byte ResourceDepletionDeltaSignal = 73;
        public const byte SaveCompletedSignal = 74;
        public const byte SaveMetadataReadySignal = 75;
        public const byte SaveRequestSignal = 76;
        public const byte SaveStatusSignal = 77;
        public const byte ScanLogChangedSignal = 78;
        public const byte ScannerToolActiveSignal = 79;
        public const byte SectorDehydratedSignal = 80;
        public const byte SectorResidencyHydratedSignal = 81;
        public const byte SimulationBucketSyncSignal = 82;
        public const byte SplashEvent = 83;
        public const byte StateCorrectionSignal = 84;
        public const byte StorageDebtSignal = 85;
        public const byte StreamingTurbulenceSignal = 86;
        public const byte SubmarineFloodStateSignal = 87;
        public const byte SubmarineLightsChangedSignal = 88;
        public const byte SurvivalVitalsChangedSignal = 89;
        public const byte SwarmDispersedSignal = 90;
        public const byte SyncFenceSignal = 91;
        public const byte SystemGlitchSignal = 92;
        public const byte SystemHealthIndexSignal = 93;
        public const byte SystemHealthSignal = 94;
        public const byte SystemPauseSignal = 95;
        public const byte TemperatureChangedSignal = 96;
        public const byte TetherFiredSignal = 97;
        public const byte TetherSnappedSignal = 98;
        public const byte TetherTensionSignal = 99;
        public const byte ThermalStateChangedSignal = 100;
        public const byte ToolLoadoutChangedSignal = 101;
        public const byte VehicleUpgradesChangedSignal = 102;
        public const byte VisorDropletSignal = 103;
        public const byte VisualFlareSignal = 104;
        public const byte VoxelCarveEvent = 105;
        public const byte WakeGeneratedSignal = 106;
        public const byte WeatherChangedSignal = 107;
        public const byte WfcOutpostDoorPowerSignal = 108;
        public const byte WfcOutpostGeneratedSignal = 109;
        public const byte WfcOutpostStateChangedSignal = 110;
        public const byte AcousticZoneChangedEvent = 111;
        public const byte DataVaultUpdateSignal = 112;
        public const byte DirectorAIMusicSignal = 113;
        public const byte HUDNotificationSignal = 114;
        public const byte PlayerExhaleSignal = 115;
        public const byte PlayerFatalPressureSignal = 116;
        public const byte PlayerFootstepSignal = 117;
        public const byte PlayerSprintStateSignal = 118;
        public const byte PlayerTransportBailoutSignal = 119;
        public const byte PlayerWaterSplashSignal = 120;
        public const byte PrefabAcousticSignatureSignal = 121;
        public const byte PrefabLoreLinkSignal = 122;
        public const byte ScalabilityChangedEvent = 123;
        public const byte SeismicSignal = 124;
        public const byte ToolAcousticSignal = 125;
        public const byte WaterTransitionSignal = 126;
        public const byte CameraJuiceImpactSignal = 127;
        public const byte DynamicMusicScalarSignal = 128;
        public const byte PlayerRespawnSignal = 129;
        public const byte SystemKillSwitchBitsSignal = 130;
        public const byte BaseStructuralWarningSignal = 131;
        public const byte ItemLifecycleSignal = 132;
        public const byte ProgressionMetaSignal = 133;
        public const byte SessionLifecycleSignal = 134;
        public const uint BaseStructuralWarningSignalStableHash = 0x42535744u;
        public const uint PlayerRespawnSignalStableHash = 0x5253504Eu;
        public const uint ScalabilityChangedEventStableHash = 0x53434C54u;
        public const uint SystemKillSwitchBitsSignalStableHash = 0x4B534257u;
        public const uint SignalLaneRegistryHash = 0x83E4FE14u;
    }
}
