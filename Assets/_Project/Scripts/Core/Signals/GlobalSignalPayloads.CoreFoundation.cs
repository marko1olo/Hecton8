using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory.Layout;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition;
using AbsoluteUniversePositionBlit = Hecton8.World.AbsoluteUniversePositionBlit;

namespace Hecton8.Core.Contracts.Signals
{
    internal static class SignalPayloadSanitizer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeFinite(float value)
        {
            return math.isfinite(value) ? value : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 SanitizeFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z)
                ? value
                : Vector3.zero;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public readonly struct ScalabilityChangedEvent : ISignal
    {
        public ScalabilityChangedEvent(byte previousTier, byte currentTier)
        {
            PreviousTier = Hecton8.Core.ScalabilityTierProfiles.Normalize(previousTier);
            CurrentTier = Hecton8.Core.ScalabilityTierProfiles.Normalize(currentTier);
            PreviousQualityTier = Hecton8.Core.ScalabilityTierRuntime.ToQualityTier(PreviousTier);
            CurrentQualityTier = Hecton8.Core.ScalabilityTierRuntime.ToQualityTier(CurrentTier);
            Reserved0 = 0u;
            Reserved1 = 0ul;
        }

        [FieldOffset(0)] public readonly byte PreviousTier;
        [FieldOffset(1)] public readonly byte CurrentTier;
        [FieldOffset(2)] public readonly Hecton8.Core.HectonQualityTier PreviousQualityTier;
        [FieldOffset(3)] public readonly Hecton8.Core.HectonQualityTier CurrentQualityTier;
        [FieldOffset(4)] public readonly uint Reserved0;
        [FieldOffset(8)] public readonly ulong Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public readonly struct AcousticZoneChangedEvent : ISignal
    {
        public AcousticZoneChangedEvent(bool isInterior)
        {
            IsInterior = isInterior ? (byte)1 : (byte)0;
            Reserved0 = 0;
            Reserved1 = 0;
            Reserved2 = 0u;
            Reserved3 = 0ul;
        }

        [FieldOffset(0)] public readonly byte IsInterior;
        [FieldOffset(1)] public readonly byte Reserved0;
        [FieldOffset(2)] public readonly ushort Reserved1;
        [FieldOffset(4)] public readonly uint Reserved2;
        [FieldOffset(8)] public readonly ulong Reserved3;
    }

    /// <summary>Scalar flood acoustic occlusion payload. Audio owns DSP; fluid owns only these bounded numbers.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HabitatFloodAcousticMuffleSignal : ISignal
    {
        public const int ExpectedCapacity = 32;
        public const int MaxFrameSignals = 32;
        public const int LowTierFrameSignals = 8;
        public const uint LaneHash = 0x464C4D46u; // FLMF
        public const byte FlagCriticalFlood = 1 << 0;
        public const byte FlagBulkheadSealed = 1 << 1;
        public const byte FlagInvalid = 1 << 7;

        [FieldOffset(0)] public long SourceGridX;
        [FieldOffset(8)] public long SourceGridY;
        [FieldOffset(16)] public long SourceGridZ;
        [FieldOffset(24)] public float3 SourceLocal;
        [FieldOffset(36)] public uint SourceHash;
        [FieldOffset(40)] public float FloodIntensity01;
        [FieldOffset(44)] public float LowPassCutoffHz;
        [FieldOffset(48)] public byte TransmissionByte;
        [FieldOffset(49)] public byte Flags;
        [FieldOffset(50)] public ushort Reserved0;
        [FieldOffset(52)] public uint Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public readonly struct DirectorAIMusicSignal : ISignal
    {
        public const byte SpawnHordeEventType = 1;
        public const byte EquipmentGlitchEventType = 2;
        public const byte RareDiscoveryEventType = 3;
        public const byte WeatherShiftEventType = 4;
        public const byte MissionTriggerEventType = 5;
        public const byte PredatorPressureEventType = 6;
        public const byte ThreatSpikeEventType = 7;

        public DirectorAIMusicSignal(byte eventType, Vector3 position, float value, bool boolValue)
        {
            Position = position;
            Value = value;
            EventType = eventType;
            BoolValue = boolValue ? (byte)1 : (byte)0;
            Reserved0 = 0;
            Reserved1 = 0u;
            Reserved2 = 0ul;
        }

        [FieldOffset(0)] public readonly Vector3 Position;
        [FieldOffset(12)] public readonly float Value;
        [FieldOffset(16)] public readonly byte EventType;
        [FieldOffset(17)] public readonly byte BoolValue;
        [FieldOffset(18)] public readonly ushort Reserved0;
        [FieldOffset(20)] public readonly uint Reserved1;
        [FieldOffset(24)] public readonly ulong Reserved2;
    }

    public enum AudioEventKind : byte
    {
        None = 0,
        AudioPing = 1,
        StructuralStress = 2
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public readonly struct AudioPingTriggerPayload
    {
        [FieldOffset(0)] public readonly long StartSampleFrame;
        [FieldOffset(8)] public readonly int SampleRate;
        [FieldOffset(12)] public readonly float Intensity;
        [FieldOffset(16)] public readonly float ChirpDurationSeconds;
        [FieldOffset(20)] public readonly Vector3 WorldPosition;
        [FieldOffset(32)] public readonly float AcousticTransmission01;
        [FieldOffset(36)] public readonly float LowPassCutoffHz;
        [FieldOffset(40)] public readonly byte Kind;
        [FieldOffset(41)] public readonly byte Reserved0;
        [FieldOffset(42)] public readonly ushort Reserved1;
        [FieldOffset(44)] public readonly uint Reserved2;

        public AudioPingTriggerPayload(
            long startSampleFrame,
            int sampleRate,
            float intensity,
            float chirpDurationSeconds,
            Vector3 worldPosition,
            float acousticTransmission01,
            float lowPassCutoffHz,
            byte kind)
        {
            StartSampleFrame = startSampleFrame;
            SampleRate = sampleRate > 0 ? sampleRate : 1;
            Intensity = math.saturate(SignalPayloadSanitizer.SanitizeFinite(intensity));
            ChirpDurationSeconds = math.max(0f, SignalPayloadSanitizer.SanitizeFinite(chirpDurationSeconds));
            WorldPosition = SignalPayloadSanitizer.SanitizeFinite(worldPosition);
            AcousticTransmission01 = math.saturate(SignalPayloadSanitizer.SanitizeFinite(acousticTransmission01, 1f));
            LowPassCutoffHz = math.clamp(SignalPayloadSanitizer.SanitizeFinite(lowPassCutoffHz, 22000f), 80f, 22000f);
            Kind = kind;
            Reserved0 = 0;
            Reserved1 = 0;
            Reserved2 = 0u;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public readonly struct StructuralStressAudioPayload
    {
        public const uint FlagHasSourceAup = 1u << 0;

        [FieldOffset(0)] public readonly AcousticAup SourceAup;
        [FieldOffset(40)] public readonly ulong SourceAupPad;
        [FieldOffset(48)] public readonly Vector3 WorldPosition;
        [FieldOffset(60)] public readonly float Stress01;
        [FieldOffset(64)] public readonly float PitchScale;
        [FieldOffset(68)] public readonly float PressureDelta;
        [FieldOffset(72)] public readonly float DepthMeters;
        [FieldOffset(76)] public readonly float AcousticTransmission01;
        [FieldOffset(80)] public readonly float LowPassCutoffHz;
        [FieldOffset(84)] public readonly float AcousticDelaySeconds;
        [FieldOffset(88)] public readonly uint Flags;
        [FieldOffset(92)] public readonly uint Reserved0;

        public StructuralStressAudioPayload(
            in AcousticAup sourceAup,
            Vector3 worldPosition,
            float stress01,
            float pitchScale,
            float pressureDelta,
            float depthMeters,
            float acousticTransmission01,
            float lowPassCutoffHz,
            float acousticDelaySeconds,
            uint flags)
        {
            SourceAup = sourceAup;
            SourceAupPad = 0ul;
            WorldPosition = SignalPayloadSanitizer.SanitizeFinite(worldPosition);
            Stress01 = math.saturate(SignalPayloadSanitizer.SanitizeFinite(stress01));
            PitchScale = math.max(0.1f, SignalPayloadSanitizer.SanitizeFinite(pitchScale, 1f));
            PressureDelta = SignalPayloadSanitizer.SanitizeFinite(pressureDelta);
            DepthMeters = math.max(0f, SignalPayloadSanitizer.SanitizeFinite(depthMeters));
            AcousticTransmission01 = math.saturate(SignalPayloadSanitizer.SanitizeFinite(acousticTransmission01, 1f));
            LowPassCutoffHz = math.clamp(SignalPayloadSanitizer.SanitizeFinite(lowPassCutoffHz, 22000f), 80f, 22000f);
            AcousticDelaySeconds = math.max(0f, SignalPayloadSanitizer.SanitizeFinite(acousticDelaySeconds));
            Flags = flags;
            Reserved0 = 0u;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct AudioEvent : ISignal
    {
        [FieldOffset(0)]
        public AudioEventKind Kind;
        [FieldOffset(1)]
        public byte Reserved0;
        [FieldOffset(2)]
        public ushort Reserved1;
        [FieldOffset(4)]
        public uint Reserved2;
        [FieldOffset(8)]
        public ulong Reserved3;
        [FieldOffset(16)]
        public AudioPingTriggerPayload AudioPing;
        [FieldOffset(16)]
        public StructuralStressAudioPayload StructuralStress;
        [FieldOffset(112)]
        private ulong _padTail0;
        [FieldOffset(120)]
        private ulong _padTail1;

        public static AudioEvent FromAudioPing(in AudioPingTriggerPayload info)
        {
            return new AudioEvent
            {
                Kind = AudioEventKind.AudioPing,
                AudioPing = info,
                Reserved0 = 0,
                Reserved1 = 0,
                Reserved2 = 0u,
                Reserved3 = 0ul
            };
        }

        public static AudioEvent FromStructuralStress(in StructuralStressAudioPayload info)
        {
            return new AudioEvent
            {
                Kind = AudioEventKind.StructuralStress,
                StructuralStress = info,
                Reserved0 = 0,
                Reserved1 = 0,
                Reserved2 = 0u,
                Reserved3 = 0ul
            };
        }

        public static AudioEvent FromStructuralStress(Vector3 worldPosition, float stress01, float pitchScale)
        {
            StructuralStressAudioPayload info = new StructuralStressAudioPayload(
                default,
                worldPosition,
                stress01,
                pitchScale,
                0f,
                0f,
                1f,
                22000f,
                0f,
                0u);
            return FromStructuralStress(in info);
        }

        private static float SanitizeFinite(float value, float fallback = 0f)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static Vector3 SanitizeFinite(Vector3 value)
        {
            return new Vector3(
                SanitizeFinite(value.x),
                SanitizeFinite(value.y),
                SanitizeFinite(value.z));
        }
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DataVaultUpdateSignal : ISignal
    {
        [FieldOffset(0)]
        public uint SourceHash;
        [FieldOffset(4)]
        public uint FieldHash;
        [FieldOffset(8)]
        public int OffsetBytes;
        [FieldOffset(12)]
        public float OldValue;
        [FieldOffset(16)]
        public float NewValue;
        [FieldOffset(20)]
        public uint Frame;
        [FieldOffset(24)]
        public ushort BufferId;
        [FieldOffset(26)]
        public ushort Flags;
        [FieldOffset(28)]
        public uint Reserved0;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PrefabAcousticSignatureSignal : ISignal
    {
        [FieldOffset(0)]
        public uint PrefabHash;
        [FieldOffset(4)]
        public uint AcousticSignatureHash;
        [FieldOffset(8)]
        public uint LoreHash;
        [FieldOffset(12)]
        public uint Frame;
        [FieldOffset(16)]
        public float Resonance01;
        [FieldOffset(20)]
        public uint OneDimensionalLutHash;
        [FieldOffset(24)]
        public ushort Flags;
        [FieldOffset(26)]
        public ushort Reserved;
        [FieldOffset(28)]
        public uint Reserved1;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PrefabLoreLinkSignal : ISignal
    {
        [FieldOffset(0)]
        public uint PrefabHash;
        [FieldOffset(4)]
        public uint LoreHash;
        [FieldOffset(8)]
        public uint Frame;
        [FieldOffset(12)]
        public uint OneDimensionalLutHash;
        [FieldOffset(16)]
        public uint HighTierVisualHash;
        [FieldOffset(20)]
        public ushort Flags;
        [FieldOffset(22)]
        public ushort Reserved;
        [FieldOffset(24)]
        public uint Reserved1;
        [FieldOffset(28)]
        public uint Reserved2;
    }

    [Preserve]
    public enum DebugSignalKind : uint
    {
        PointerLink = 1u,
        GenerationId = 2u,
        CollisionNormal = 3u,
        BreadcrumbSegment = 4u,
        GasRoom = 5u,
        PressureVector = 6u,
        FluidVelocity = 7u,
        AcousticRay = 8u,
        SignalEvent = 9u,
        LaneSaturation = 10u,
        EventResonance = 11u,
        NanGeyser = 12u,
        Homeostasis = 13u,
        GhostPose = 14u,
        VramBudgetSlice = 15u,
        AupTeleportPreview = 16u
    }

    [Preserve]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DebugSignal : ISignal
    {
        [FieldOffset(0)]
        public uint Kind;
        [FieldOffset(4)]
        public uint EntityId;
        [FieldOffset(8)]
        public uint ProducerId;
        [FieldOffset(12)]
        public uint ConsumerId;
        [FieldOffset(16)]
        public float3 Position;
        [FieldOffset(28)]
        public float3 Vector;
        [FieldOffset(40)]
        public float Value0;
        [FieldOffset(44)]
        public float Value1;
        [FieldOffset(48)]
        public uint Flags;
        [FieldOffset(52)]
        public uint Frame;
        [FieldOffset(56)]
        public uint Aux0;
        [FieldOffset(60)]
        public uint Aux1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SystemHealthSignal : ISignal
    {
        public const int ExpectedCapacity = 16;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 0x48484C54u; // HHLT

        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public float SystemHealthIndex01;
        [FieldOffset(8)]
        public float FpsEwma;
        [FieldOffset(12)]
        public float JitterSigmaMs;
        [FieldOffset(16)]
        public float CpuTempC;
        [FieldOffset(20)]
        public float GpuUtil01;
        [FieldOffset(24)]
        public float BatteryLife01;
        [FieldOffset(28)]
        public uint Reserved0;
        [FieldOffset(32)]
        public ulong KillSwitchMask;
        [FieldOffset(40)]
        public byte PressureLevel;
        [FieldOffset(41)]
        public byte FoveatedPressureTier;
        [FieldOffset(42)]
        public ushort Flags;
        [FieldOffset(44)]
        public uint Reserved1;
        [FieldOffset(48)]
        public ulong Reserved2;
        [FieldOffset(56)]
        public ulong Reserved3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ReentryVfxStateSignal : ISignal
    {
        public const int ExpectedCapacity = 4;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 4;
        public const uint LaneHash = 0xAB8A9BF1u; // FNV32("ReentryVfxStateSignal")
        public const byte FlagLowTier = 1 << 0;
        public const byte FlagWhiteout = 1 << 1;
        public const byte FlagHydrated = 1 << 2;
        public const byte FlagNaNGuard = 1 << 3;
        public const byte FlagSpatialAnchor = 1 << 4;

        [FieldOffset(0)]
        public AbsoluteUniversePosition CapsuleAup;
        [FieldOffset(48)]
        public float Heat01;
        [FieldOffset(52)]
        public float Opacity01;
        [FieldOffset(56)]
        public ushort Sequence;
        [FieldOffset(58)]
        public ushort HydrationSequence;
        [FieldOffset(60)]
        public byte Phase;
        [FieldOffset(61)]
        public byte Flags;
        [FieldOffset(62)]
        public byte QualityTier;
        [FieldOffset(63)]
        public byte Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VisorDropletSignal : ISignal
    {
        public const int ExpectedCapacity = 8;
        public const int MaxFrameSignals = 32;
        public const int LowTierFrameSignals = 8;
        public const uint LaneHash = 0x5A0A8332u; // FNV32("VisorDropletSignal")
        public const byte DropletKindMassiveSplash = 1;
        public const byte FlagExternalSplash = 1 << 0;

        [FieldOffset(0)]
        public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)]
        public float Intensity01;
        [FieldOffset(52)]
        public float DurationSeconds;
        [FieldOffset(56)]
        public uint SourceHash;
        [FieldOffset(60)]
        public byte DropletKind;
        [FieldOffset(61)]
        public byte Flags;
        [FieldOffset(62)]
        public ushort Sequence;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct InputSignal : ISignal
    {
        [FieldOffset(0)]
        public float2 MoveDelta;
        [FieldOffset(8)]
        public float2 LookDelta;
        [FieldOffset(16)]
        public float VerticalDelta;
        [FieldOffset(20)]
        public uint ActionsBitmask;
        [FieldOffset(24)]
        public uint CurrentInputSchemeHash;
        [FieldOffset(28)]
        public uint Frame;
        [FieldOffset(32)]
        public uint Sequence;
        [FieldOffset(36)]
        public byte Flags;
        [FieldOffset(37)]
        public byte Reserved0;
        [FieldOffset(38)]
        public ushort Reserved1;
        [FieldOffset(40)]
        public ulong Reserved2;
        [FieldOffset(48)]
        public ulong Reserved3;
        [FieldOffset(56)]
        public ulong Reserved4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct StateCorrectionSignal : ISignal
    {
        [FieldOffset(0)]
        public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)]
        public float3 RuntimePosition;
        [FieldOffset(60)]
        public float3 Velocity;
        [FieldOffset(72)]
        public quaternion Rotation;
        [FieldOffset(88)]
        public uint AuthoritativeHash;
        [FieldOffset(92)]
        public uint ExpectedLocalHash;
        [FieldOffset(96)]
        public uint Frame;
        [FieldOffset(100)]
        public uint SourceId;
        [FieldOffset(104)]
        public uint Sequence;
        [FieldOffset(108)]
        public byte Flags;
        [FieldOffset(109)]
        public byte Reserved0;
        [FieldOffset(110)]
        public ushort Reserved1;
        [FieldOffset(112)]
        public uint Reserved2;
        [FieldOffset(116)]
        public uint Reserved3;
        [FieldOffset(120)]
        public ulong Reserved4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct DesyncDetectedSignal : ISignal
    {
        [FieldOffset(0)]
        public uint LocalHash;
        [FieldOffset(4)]
        public uint AuthoritativeHash;
        [FieldOffset(8)]
        public uint Frame;
        [FieldOffset(12)]
        public uint SourceId;
        [FieldOffset(16)]
        public uint LastFenceFrame;
        [FieldOffset(20)]
        public byte Flags;
        [FieldOffset(21)]
        public byte Reserved0;
        [FieldOffset(22)]
        public ushort Reserved1;
        [FieldOffset(24)]
        public uint Reserved2;
        [FieldOffset(28)]
        public uint Reserved3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct SyncFenceSignal : ISignal
    {
        [FieldOffset(0)]
        public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)]
        public float3 RuntimePosition;
        [FieldOffset(60)]
        public float3 Velocity;
        [FieldOffset(72)]
        public quaternion Rotation;
        [FieldOffset(88)]
        public uint StateHash;
        [FieldOffset(92)]
        public uint Frame;
        [FieldOffset(96)]
        public uint SourceId;
        [FieldOffset(100)]
        public uint Sequence;
        [FieldOffset(104)]
        public byte Flags;
        [FieldOffset(105)]
        public byte Reserved0;
        [FieldOffset(106)]
        public ushort Reserved1;
        [FieldOffset(108)]
        public uint Reserved2;
        [FieldOffset(112)]
        public ulong Reserved3;
        [FieldOffset(120)]
        public ulong Reserved4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct KccVelocitySignal : ISignal
    {
        public const byte FlagQualityPressureLegacy = 1 << 0;
        public const byte FlagMovementAuthorityExternal = 1 << 1;

        [FieldOffset(0)]
        public AbsoluteUniversePosition BodyAup;
        [FieldOffset(48)]
        public float3 Velocity;
        [FieldOffset(60)]
        public float PlanarSpeedSq;
        [FieldOffset(64)]
        public uint Frame;
        [FieldOffset(68)]
        public uint SourceId;
        [FieldOffset(72)]
        public uint Sequence;
        [FieldOffset(76)]
        public byte Flags;
        [FieldOffset(77)]
        public byte QualityPressureQ8;
        [FieldOffset(78)]
        public ushort Reserved1;
        [FieldOffset(80)]
        public ulong Reserved2;
        [FieldOffset(88)]
        public ulong Reserved3;
        [FieldOffset(96)]
        public ulong Reserved4;
        [FieldOffset(104)]
        public ulong Reserved5;
        [FieldOffset(112)]
        public ulong Reserved6;
        [FieldOffset(120)]
        public ulong Reserved7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    public struct TetherTensionSignal : ISignal
    {
        [FieldOffset(0)]
        public AbsoluteUniversePosition AnchorAup;
        [FieldOffset(48)]
        public AbsoluteUniversePosition PayloadAup;
        [FieldOffset(96)]
        public float3 DirectionToPayload;
        [FieldOffset(108)]
        public uint TetherId;
        [FieldOffset(112)]
        public uint FrameIndex;
        [FieldOffset(116)]
        public float TensionForce;
        [FieldOffset(120)]
        public float SnapThreshold;
        [FieldOffset(124)]
        public float Tension01;
        [FieldOffset(128)]
        public float ReactiveVfx01;
        [FieldOffset(132)]
        public ushort NodeCount;
        [FieldOffset(134)]
        public byte Flags;
        [FieldOffset(135)]
        public byte Reserved;
        [FieldOffset(136)]
        public uint ReservedTail0;
        [FieldOffset(140)]
        public uint ReservedTail1;
        [FieldOffset(144)]
        public ulong ReservedTail2;
        [FieldOffset(152)]
        public ulong ReservedTail3;
        [FieldOffset(160)]
        public ulong ReservedTail4;
        [FieldOffset(168)]
        public ulong ReservedTail5;
        [FieldOffset(176)]
        public ulong ReservedTail6;
        [FieldOffset(184)]
        public ulong ReservedTail7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct TetherSnappedSignal : ISignal
    {
        [FieldOffset(0)]
        public AbsoluteUniversePosition SnapAup;
        [FieldOffset(48)]
        public uint TetherId;
        [FieldOffset(52)]
        public uint FrameIndex;
        [FieldOffset(56)]
        public float PeakTension;
        [FieldOffset(60)]
        public float SnapThreshold;
        [FieldOffset(64)]
        public float Severity01;
        [FieldOffset(68)]
        public ushort NodeCount;
        [FieldOffset(70)]
        public byte Reason;
        [FieldOffset(71)]
        public byte Flags;
        [FieldOffset(72)]
        public ulong ReservedPadding;
        [FieldOffset(80)]
        public ulong ReservedPadding1;
        [FieldOffset(88)]
        public ulong ReservedPadding2;
        [FieldOffset(96)]
        public ulong ReservedPadding3;
        [FieldOffset(104)]
        public ulong ReservedPadding4;
        [FieldOffset(112)]
        public ulong ReservedPadding5;
        [FieldOffset(120)]
        public ulong ReservedPadding6;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TetherFiredSignal : ISignal
    {
        [FieldOffset(0)]
        public int ManagerInstanceId;
        [FieldOffset(4)]
        public int OwnerInstanceId;
        [FieldOffset(8)]
        public int PayloadBodyInstanceId;
        [FieldOffset(12)]
        public int PayloadColliderInstanceId;
        [FieldOffset(16)]
        public int RequestSlot;
        [FieldOffset(20)]
        public uint RequestVersion;
        [FieldOffset(24)]
        public uint FrameIndex;
        [FieldOffset(28)]
        public float InitialDistance;
        [FieldOffset(32)]
        public uint Flags;
        [FieldOffset(36)]
        public uint Reserved;
        [FieldOffset(40)]
        public ulong ReservedPadding;
        [FieldOffset(48)]
        public ulong ReservedPadding1;
        [FieldOffset(56)]
        public ulong ReservedPadding2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct DockingRequestSignal : ISignal
    {
        [FieldOffset(0)] public int DroneId;
        [FieldOffset(4)] public int HubGridId;
        [FieldOffset(8)] public AbsoluteUniversePositionBlit DockAup;
        [FieldOffset(56)] public float3 DockForward;
        [FieldOffset(68)] public uint RequestId;
        [FieldOffset(72)] public byte Flags;
        [FieldOffset(73)] public byte Reserved0;
        [FieldOffset(74)] public byte Reserved1;
        [FieldOffset(75)] public byte Reserved2;
        [FieldOffset(76)] public uint ReservedTail;
        [FieldOffset(80)] public ulong ReservedTail1;
        [FieldOffset(88)] public ulong ReservedTail2;
        [FieldOffset(96)] public ulong ReservedTail3;
        [FieldOffset(104)] public ulong ReservedTail4;
        [FieldOffset(112)] public ulong ReservedTail5;
        [FieldOffset(120)] public ulong ReservedTail6;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct DockingCompleteSignal : ISignal
    {
        [FieldOffset(0)] public int DroneId;
        [FieldOffset(4)] public int HubGridId;
        [FieldOffset(8)] public AbsoluteUniversePositionBlit DockAup;
        [FieldOffset(56)] public float3 DockForward;
        [FieldOffset(68)] public uint RequestId;
        [FieldOffset(72)] public byte Flags;
        [FieldOffset(73)] public byte Reserved0;
        [FieldOffset(74)] public byte Reserved1;
        [FieldOffset(75)] public byte Reserved2;
        [FieldOffset(76)] public uint ReservedTail;
        [FieldOffset(80)] public ulong ReservedTail1;
        [FieldOffset(88)] public ulong ReservedTail2;
        [FieldOffset(96)] public ulong ReservedTail3;
        [FieldOffset(104)] public ulong ReservedTail4;
        [FieldOffset(112)] public ulong ReservedTail5;
        [FieldOffset(120)] public ulong ReservedTail6;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct DockingFailedSignal : ISignal
    {
        [FieldOffset(0)] public int DroneId;
        [FieldOffset(4)] public int HubGridId;
        [FieldOffset(8)] public AbsoluteUniversePositionBlit LastAup;
        [FieldOffset(56)] public float3 FailureVector;
        [FieldOffset(68)] public uint RequestId;
        [FieldOffset(72)] public byte Reason;
        [FieldOffset(73)] public byte Flags;
        [FieldOffset(74)] public byte Reserved0;
        [FieldOffset(75)] public byte Reserved1;
        [FieldOffset(76)] public uint ReservedTail;
        [FieldOffset(80)] public ulong ReservedTail1;
        [FieldOffset(88)] public ulong ReservedTail2;
        [FieldOffset(96)] public ulong ReservedTail3;
        [FieldOffset(104)] public ulong ReservedTail4;
        [FieldOffset(112)] public ulong ReservedTail5;
        [FieldOffset(120)] public ulong ReservedTail6;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct VoxelCarveEvent : ISignal
    {
        [FieldOffset(0)]
        public ulong VolumeInstanceId;
        [FieldOffset(8)]
        public float3 AbsoluteHitPoint;
        [FieldOffset(20)]
        public float3 AbsoluteSegmentEnd;
        [FieldOffset(32)]
        public float3 AbsoluteHalfExtents;
        [FieldOffset(44)]
        public float3 AbsoluteImpulseDirection;
        [FieldOffset(56)]
        public double3 AbsoluteHitPointDouble;
        [FieldOffset(80)]
        public double3 AbsoluteSegmentEndDouble;
        [FieldOffset(104)]
        public float RadiusMeters;
        [FieldOffset(108)]
        public float BlendStrengthMeters;
        [FieldOffset(112)]
        public byte Operation;
        [FieldOffset(113)]
        public byte Shape;
        [FieldOffset(114)]
        public byte MaterialId;
        [FieldOffset(115)]
        public byte SourceFlags;
        [FieldOffset(116)]
        public uint Reserved0;
        [FieldOffset(120)]
        public uint Reserved1;
        [FieldOffset(124)]
        public uint Reserved2;
    }

    /// <summary>
    /// Broadcast signal emitted when a shaft source resolves as a burst-grade bioluminescent flare.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VisualFlareSignal : ISignal
    {
        public const int ExpectedCapacity = 16;
        public const int MaxFrameSignals = 16;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 0x56464C52u; // VFLR

        /// <summary>Stable source ID or component instance fallback.</summary>
        [FieldOffset(0)]
        public uint SourceId;
        /// <summary>Resolved burst intensity after LOD and distance gates.</summary>
        [FieldOffset(4)]
        public float Intensity01;
        /// <summary>Viewport-space source position.</summary>
        [FieldOffset(8)]
        public float2 ScreenUv;
        /// <summary>Unity frame index at emission.</summary>
        [FieldOffset(16)]
        public uint Frame;
        /// <summary>Bitfield reserved for source kind and debug state.</summary>
        [FieldOffset(20)]
        public byte Flags;
        [FieldOffset(21)]
        public byte Reserved0;
        [FieldOffset(22)]
        public ushort Reserved1;
        [FieldOffset(24)]
        public ulong Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct CameraJuiceImpactSignal : ISignal
    {
        public const uint ProfileHighFreqToolVibrationHash = 3014650645u; // FNV1A("HighFreqToolVibration")
        public const uint ProfileLowFreqSeismicHeaveHash = 335997281u; // FNV1A("LowFreqSeismicHeave")
        public const uint ProfileSharpKineticImpactHash = 1680791348u; // FNV1A("SharpKineticImpact")
        public const uint ProfileContinuousPressureStressHash = 3689851005u; // FNV1A("ContinuousPressureStress")

        [FieldOffset(0)] public ImpactSignal Impact;
        [FieldOffset(64)] public float3 Direction;
        [FieldOffset(76)] public float Severity;
        [FieldOffset(80)] public uint ProfileHash;
        [FieldOffset(84)] public uint SourceHash;
        [FieldOffset(88)] public float AmplitudeScale;
        [FieldOffset(92)] public float RadiusOverrideMeters;
        [FieldOffset(96)] public float TranslationGain;
        [FieldOffset(100)] public float RotationGain;
        [FieldOffset(104)] public byte Priority;
        [FieldOffset(105)] public byte Flags;
        [FieldOffset(106)] public ushort Reserved0;
        [FieldOffset(108)] public uint Reserved1;
        [FieldOffset(112)] public ulong Reserved2;
        [FieldOffset(120)] public ulong Reserved3;
    }

    /// <summary>
    /// Deterministic two-stage initialization contract for registry-pinned systems.
    /// </summary>
    [Preserve]
    public interface IInitializable
    {
        /// <summary>Registers the system with its owning registry without resolving external dependencies.</summary>
        void OnRegister();

        /// <summary>Resolves and caches external dependencies after all systems are registered.</summary>
        void OnDependencyInject();
    }

    /// <summary>
    /// In-place signal snapshot transformer. Used for rare structural passes such as AUP rebases.
    /// </summary>
    /// <typeparam name="T">Signal payload type.</typeparam>
    [Preserve]
    public interface ISignalSnapshotTransformer<T>
        where T : unmanaged, ISignal
    {
        /// <summary>Transforms one signal payload in-place.</summary>
        /// <param name="signal">Signal payload to mutate.</param>
        void Transform(ref T signal);
    }

    /// <summary>
    /// In-place signal snapshot filter. Used for tombstone/alive-mask passes before consumers read a lane.
    /// </summary>
    /// <typeparam name="T">Signal payload type.</typeparam>
    [Preserve]
    public interface ISignalSnapshotFilter<T>
        where T : unmanaged, ISignal
    {
        /// <summary>Returns true when the signal remains visible to consumers.</summary>
        /// <param name="signal">Signal payload to inspect.</param>
        bool Keep(in T signal);
    }

    /// <summary>
    /// Pre-simulation deterministic input snapshot. Gameplay physics consumes this signal, not hardware APIs.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InputStateSignal : ISignal
    {
        [FieldOffset(0)]
        public Hecton8.Core.InputState State;
        [FieldOffset(24)]
        public uint CurrentInputSchemeHash;
        [FieldOffset(28)]
        public byte InputDelayFrames;
        [FieldOffset(29)]
        public byte AppliedDelayFrames;
        [FieldOffset(30)]
        public ushort Flags;
    }

    /// <summary>
    /// Master state hash fence published by the lockstep validator after a completed deterministic sample.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct LockstepSnapshotSignal : ISignal
    {
        [FieldOffset(0)]
        public ulong MasterHash;
        [FieldOffset(8)]
        public uint Frame;
        [FieldOffset(12)]
        public uint HashCadenceFrames;
        [FieldOffset(16)]
        public uint Flags;
        [FieldOffset(20)]
        public uint MissingMask;
        [FieldOffset(24)]
        public uint NonFiniteMask;
        [FieldOffset(28)]
        public uint ReplayBlock;
    }

    /// <summary>
    /// Developer-facing glitch pulse emitted when deterministic replay validation fails.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SystemGlitchSignal : ISignal
    {
        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public uint SourceId;
        [FieldOffset(8)]
        public uint LocalHash;
        [FieldOffset(12)]
        public uint ExpectedHash;
        [FieldOffset(16)]
        public float Intensity01;
        [FieldOffset(20)]
        public float DurationSeconds;
        [FieldOffset(24)]
        public byte Reason;
        [FieldOffset(25)]
        public byte Flags;
        [FieldOffset(26)]
        public ushort Reserved0;
        [FieldOffset(28)]
        public uint Reserved1;
    }

    /// <summary>
    /// Laser cutter event kind carried by <see cref="LaserCutterEventPayload"/>.
    /// </summary>
    public enum LaserCutterEventType : byte
    {
        /// <summary>Normalized heat value changed beyond the publish threshold.</summary>
        HeatChanged = 0,

        /// <summary>Beam activation state changed.</summary>
        BeamStateChanged = 1
    }

    /// <summary>
    /// Blittable laser cutter event payload queued by the cutter typed lane.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct LaserCutterEventPayload : ISignal
    {
        public const ushort StateFlagBeamActive = 1;

        /// <summary>Normalized heat value [0, 1].</summary>
        [FieldOffset(0)]
        public float Heat01;

        /// <summary>Runtime entity id hash of the cutter source.</summary>
        [FieldOffset(4)]
        public int CutterInstanceId;

        /// <summary>Runtime entity id hash of the cutter root transform.</summary>
        [FieldOffset(8)]
        public int CutterRootInstanceId;

        /// <summary>Serialized <see cref="LaserCutterEventType"/> value.</summary>
        [FieldOffset(12)]
        public ushort EventType;

        /// <summary>Bit flags for event-specific state.</summary>
        [FieldOffset(14)]
        public ushort StateFlags;
    }

    /// <summary>
    /// Deferred exterior water-entry payload emitted by sampled hull buoyancy points.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SplashEvent : ISignal
    {
        /// <summary>Camera-relative world position of the splash contact point.</summary>
        [FieldOffset(0)]
        public float3 RuntimePosition;

        /// <summary>Absolute universe position of the splash for persistent VFX anchoring.</summary>
        [FieldOffset(12)]
        public float3 AbsoluteUniversePosition;

        /// <summary>Water surface normal at the splash point.</summary>
        [FieldOffset(24)]
        public float3 SurfaceNormal;

        /// <summary>Vertical impact speed at the moment of water entry.</summary>
        [FieldOffset(36)]
        public float ImpactSpeedMetersPerSecond;

        /// <summary>Kinetic energy of the impact in joules, used to scale splash VFX intensity.</summary>
        [FieldOffset(40)]
        public float KineticEnergyJoules;

        /// <summary>0-1 ratio of the sample point submerged below the waterline at impact.</summary>
        [FieldOffset(44)]
        public float SubmersionFactor;

        /// <summary>Index of the exterior buoyancy sample point that detected the splash.</summary>
        [FieldOffset(48)]
        public int SampleIndex;

        [FieldOffset(52)]
        public uint Reserved0;

        [FieldOffset(56)]
        public uint Reserved1;

        [FieldOffset(60)]
        public uint Reserved2;
    }

    /// <summary>
    /// Physics event discriminator for <see cref="PhysicsEventPayload"/>.
    /// </summary>
    public enum PhysicsEventType : ushort
    {
        PressureImpulse = 1,
        ElectromagneticPulse = 2,
        AcousticPing = 3,
        AcousticImpulse = 4,
        FloodMassShift = 5
    }

    /// <summary>
    /// Unmanaged event payload carried by the deferred physics event lane.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct PhysicsEventPayload : ISignal
    {
        [FieldOffset(0)]
        public Vector3 RuntimePosition;
        [FieldOffset(12)]
        public Vector3 Direction;
        [FieldOffset(24)]
        public Vector3 ForceVector;
        [FieldOffset(36)]
        public Vector3 ImpulseVector;
        [FieldOffset(48)]
        public float RadiusMeters;
        [FieldOffset(52)]
        public float Scalar0;
        [FieldOffset(56)]
        public float Scalar1;
        [FieldOffset(60)]
        public float Scalar2;
        [FieldOffset(64)]
        public int PrimaryId;
        [FieldOffset(68)]
        public uint DataHash;
        [FieldOffset(72)]
        public uint StatusBits;
        [FieldOffset(76)]
        public ushort EventType;
        [FieldOffset(78)]
        public ushort Reserved;
        [FieldOffset(80)]
        public ulong ReservedTail0;
        [FieldOffset(88)]
        public ulong ReservedTail1;
        [FieldOffset(96)]
        public ulong ReservedTail2;
        [FieldOffset(104)]
        public ulong ReservedTail3;
        [FieldOffset(112)]
        public ulong ReservedTail4;
        [FieldOffset(120)]
        public ulong ReservedTail5;
    }

    /// <summary>
    /// Deferred submarine hull impact payload consumed by the trauma dispatcher.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DeferredSubmarineImpactSignal : ISignal
    {
        [FieldOffset(0)]
        public float3 LocalPoint;
        [FieldOffset(12)]
        public float Magnitude;
        [FieldOffset(16)]
        public float Depth;
        [FieldOffset(20)]
        public uint DamageType;
        [FieldOffset(24)]
        public float PreviousIntegrityNormalized;
        [FieldOffset(28)]
        public float NextIntegrityNormalized;
        [FieldOffset(32)]
        public ushort SourceId;
        [FieldOffset(34)]
        public byte IntegrityDelta;
        [FieldOffset(35)]
        public byte TraumaLevel;
        [FieldOffset(36)]
        public uint Reserved0;
        [FieldOffset(40)]
        public uint Reserved1;
        [FieldOffset(44)]
        public uint Reserved2;
        [FieldOffset(48)]
        public ulong Reserved3;
        [FieldOffset(56)]
        public ulong Reserved4;
    }

    /// <summary>Discrete player input command identifiers for zero-GC UI/gameplay consumers.</summary>
    public static class PlayerInputSignalCommands
    {
        public const byte ToggleInventory = 1;
        public const byte TogglePda = 2;
        public const byte Cancel = 3;
        public const byte TabNext = 4;
        public const byte TabPrevious = 5;
        public const byte Interact = 6;
        public const byte PrimaryAction = 7;
        public const byte SecondaryAction = 8;
        public const byte ToolSlot1 = 9;
        public const byte ToolSlot2 = 10;
        public const byte ToolSlot3 = 11;
        public const byte ToolSlot4 = 12;
        public const byte Flashlight = 13;
    }

    /// <summary>Discrete player input lane for command-style consumers. Size: 32 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PlayerInputSignal : ISignal
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public uint Frame;
        [FieldOffset(8)] public uint Sequence;
        [FieldOffset(12)] public byte Command;
        [FieldOffset(13)] public byte Flags;
        [FieldOffset(31)] private byte _pad;
    }

    /// <summary>Player look target state identifiers for diegetic UI consumers.</summary>
    public static class PlayerLookTargetSignalStates
    {
        public const byte Cleared = 0;
        public const byte Acquired = 1;
    }

    /// <summary>Player kinematics look-target lane for diegetic prompts. Hash-only payload. Size: 128 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct PlayerLookTargetSignal : ISignal
    {
        [FieldOffset(0)] public AbsoluteUniversePosition TargetAup;
        [FieldOffset(48)] public float3 RuntimeAnchor;
        [FieldOffset(60)] public float DistanceMeters;
        [FieldOffset(64)] public uint TargetHash;
        [FieldOffset(68)] public uint Frame;
        [FieldOffset(72)] public uint ColliderHash;
        [FieldOffset(76)] public float3 SurfaceNormal;
        [FieldOffset(88)] public uint PromptHash;
        [FieldOffset(92)] public byte State;
        [FieldOffset(93)] public byte Flags;
        [FieldOffset(94)] private ushort _reserved;
        [FieldOffset(96)] public uint PromptArg0;
        [FieldOffset(100)] public uint PromptArg1;
        [FieldOffset(104)] public uint PromptArg2;
        [FieldOffset(108)] public uint PromptArg3;
        [FieldOffset(112)] private ulong _pad0;
        [FieldOffset(120)] private ulong _pad1;
    }


}
