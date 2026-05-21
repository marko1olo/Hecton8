using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Collections;
using Unity.Mathematics;
using AcousticAup = Hecton8.Core.Contracts.AcousticAup;

namespace Hecton8.Audio.Virtualization
{
    /// <summary>
    /// Registry-published virtual voice scheduler. Implementations must keep enqueue,
    /// sort, and selection paths allocation-free after explicit initialization.
    /// </summary>
    public interface IAudioVirtualizationService
    {
        bool IsVirtualizationReady { get; }
        int PhysicalVoiceLimit { get; }
        int VirtualVoiceCount { get; }
        int ActivePhysicalVoiceCount { get; }
        int CulledVoiceCount { get; }
        int StolenVoiceCount { get; }
        int DroppedVoiceCount { get; }
        bool EnqueueVirtualVoice(in VirtualVoiceRequest request);
        void SetVirtualListener(in AcousticAup listenerAup);
        void SetLowTierVirtualization(bool lowTier);
        void ApplyVirtualVoiceAupShift(long gridDeltaX, long gridDeltaY, long gridDeltaZ);
        bool TryGetVirtualizationStats(out VirtualVoiceStatistics statistics);
    }

    [Flags]
    public enum VirtualVoiceDspFlags : byte
    {
        None = 0,
        SdfOccluded = 1 << 0,
        InsideSubmarineHull = 1 << 1,
        SabineResolved = 1 << 2,
        Delayed = 1 << 3
    }

    [Flags]
    public enum VirtualVoicePortalFlags : byte
    {
        None = 0,
        Voxel = 1 << 0,
        Habitat = 1 << 1,
        SealedBulkhead = 1 << 2,
        Solid = 1 << 3,
        StationaryEmitter = 1 << 4
    }

    /// <summary>
    /// Exact compact DTO used by external virtual emitters before AUP/grid expansion.
    /// Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct VirtualVoiceDTO
    {
        [FieldOffset(0)]
        public double3 AupMeters;
        [FieldOffset(24)]
        public float Volume;
        [FieldOffset(28)]
        public float Pitch;
        [FieldOffset(32)]
        public uint ClipHash;
        [FieldOffset(36)]
        public uint SourceEntityID;
        [FieldOffset(40)]
        public float Importance;
        [FieldOffset(44)]
        public uint Padding;
    }

    /// <summary>
    /// One-cache-line source DTO for SHINOBU SDF acoustic occlusion kernels.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AcousticSourceDTO
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public float BaseVolume;
        [FieldOffset(8)] public float BasePitch;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public double3 AUP_Position;
        [FieldOffset(40)] public float ComputedOcclusion;
        [FieldOffset(44)] public float ComputedReverb;
        [FieldOffset(48)] private uint _pad0;
        [FieldOffset(52)] private uint _pad1;
        [FieldOffset(56)] private uint _pad2;
        [FieldOffset(60)] private uint _pad3;
    }

    /// <summary>
    /// One-cache-line DSP upload row produced by the analytical SDF acoustic kernel.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AcousticDspOutputDTO
    {
        [FieldOffset(0)] public uint SourceHash;
        [FieldOffset(4)] public float Volume;
        [FieldOffset(8)] public float Pitch;
        [FieldOffset(12)] public float Occlusion01;
        [FieldOffset(16)] public float ReverbRt60Seconds;
        [FieldOffset(20)] public float LowPassHertz;
        [FieldOffset(24)] public float DelaySeconds;
        [FieldOffset(28)] public float DopplerRatio;
        [FieldOffset(32)] public float ItdSeconds;
        [FieldOffset(36)] public float Ild01;
        [FieldOffset(40)] public float DistanceSq;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] private uint _pad0;
        [FieldOffset(52)] private uint _pad1;
        [FieldOffset(56)] private uint _pad2;
        [FieldOffset(60)] private uint _pad3;
    }

    /// <summary>
    /// Compact material absorption row for cold-loaded or emergency-mock acoustics.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AcousticMaterialCoefficientDTO
    {
        [FieldOffset(0)] public uint MaterialHash;
        [FieldOffset(4)] public float Absorption01;
        [FieldOffset(8)] public float Scatter01;
        [FieldOffset(12)] public float Density01;
        [FieldOffset(16)] public float LowPassHertz;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] private uint _pad0;
        [FieldOffset(28)] private uint _pad1;
    }

    /// <summary>
    /// Blittable request queued by gameplay audio emitters before physical channel selection.
    /// Explicit size preserves ARM64-friendly stride while embedding the aligned AcousticAup contract.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct VirtualVoiceRequest
    {
        [FieldOffset(0)]
        public AcousticAup SourceAup;
        [FieldOffset(40)]
        public float3 SourceVelocityMetersPerSecond;
        [FieldOffset(52)]
        public float Volume;
        [FieldOffset(56)]
        public float Priority;
        [FieldOffset(60)]
        public float Pitch;
        [FieldOffset(64)]
        public float DopplerRatio;
        [FieldOffset(68)]
        public float SabineRt60Seconds;
        [FieldOffset(72)]
        public float SabineRoomVolumeCubicMeters;
        [FieldOffset(76)]
        public float LowPassCutoffHz;
        [FieldOffset(80)]
        public float DelaySeconds;
        [FieldOffset(84)]
        public uint EventID;
        [FieldOffset(88)]
        public uint ClipHash;
        [FieldOffset(92)]
        public uint SourceEntityID;
        [FieldOffset(96)]
        public int StationaryCacheKey;
        [FieldOffset(100)]
        public VirtualVoicePortalFlags PortalFlags;
        [FieldOffset(101)]
        public byte FoveatedTier;
        [FieldOffset(102)]
        public byte AcousticEnvironment;
        [FieldOffset(103)]
        public VirtualVoiceDspFlags DspFlags;
        [FieldOffset(104)]
        private byte _reserved0;
        [FieldOffset(108)]
        private uint _reserved1;

        public VirtualVoiceRequest(
            uint eventID,
            uint clipHash,
            in AcousticAup sourceAup,
            float volume,
            float priority,
            float pitch,
            float dopplerRatio,
            int stationaryCacheKey,
            VirtualVoicePortalFlags portalFlags,
            byte foveatedTier)
            : this(
                eventID,
                clipHash,
                0u,
                in sourceAup,
                float3.zero,
                volume,
                priority,
                pitch,
                dopplerRatio,
                0f,
                0f,
                VirtualVoiceUtility.OpenLowPassHertz,
                0f,
                stationaryCacheKey,
                portalFlags,
                foveatedTier,
                0,
                VirtualVoiceDspFlags.None)
        {
        }

        public VirtualVoiceRequest(
            uint eventID,
            uint clipHash,
            uint sourceEntityID,
            in AcousticAup sourceAup,
            float3 sourceVelocityMetersPerSecond,
            float volume,
            float priority,
            float pitch,
            float dopplerRatio,
            float sabineRt60Seconds,
            float sabineRoomVolumeCubicMeters,
            float lowPassCutoffHz,
            float delaySeconds,
            int stationaryCacheKey,
            VirtualVoicePortalFlags portalFlags,
            byte foveatedTier,
            byte acousticEnvironment,
            VirtualVoiceDspFlags dspFlags)
        {
            EventID = eventID;
            ClipHash = clipHash;
            SourceEntityID = sourceEntityID;
            SourceAup = sourceAup;
            SourceVelocityMetersPerSecond = sourceVelocityMetersPerSecond;
            Volume = volume;
            Priority = priority;
            Pitch = pitch;
            DopplerRatio = dopplerRatio;
            SabineRt60Seconds = sabineRt60Seconds;
            SabineRoomVolumeCubicMeters = sabineRoomVolumeCubicMeters;
            LowPassCutoffHz = lowPassCutoffHz;
            DelaySeconds = delaySeconds;
            StationaryCacheKey = stationaryCacheKey;
            PortalFlags = portalFlags;
            FoveatedTier = foveatedTier;
            AcousticEnvironment = acousticEnvironment;
            DspFlags = dspFlags;
            _reserved0 = 0;
            _reserved1 = 0;
        }
    }

    /// <summary>
    /// Mutable virtual voice state consumed by the Burst ranking job.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 160)]
    public struct VirtualVoice
    {
        [FieldOffset(0)]
        public AcousticAup SourceAup;
        [FieldOffset(40)]
        public float3 SourceVelocityMetersPerSecond;
        [FieldOffset(52)]
        public float Volume;
        [FieldOffset(56)]
        public float Priority;
        [FieldOffset(60)]
        public float Pitch;
        [FieldOffset(64)]
        public float DopplerRatio;
        [FieldOffset(68)]
        public float Attenuation;
        [FieldOffset(72)]
        public float Weight;
        [FieldOffset(76)]
        public float DistanceSq;
        [FieldOffset(80)]
        public float EffectiveVolume;
        [FieldOffset(84)]
        public float SabineRt60Seconds;
        [FieldOffset(88)]
        public float SabineRoomVolumeCubicMeters;
        [FieldOffset(92)]
        public float LowPassCutoffHz;
        [FieldOffset(96)]
        public float DelaySeconds;
        [FieldOffset(100)]
        public uint EventID;
        [FieldOffset(104)]
        public uint ClipHash;
        [FieldOffset(108)]
        public uint StableKey;
        [FieldOffset(112)]
        public uint SourceEntityID;
        [FieldOffset(116)]
        public int StationaryCacheKey;
        [FieldOffset(120)]
        public VirtualVoicePortalFlags PortalFlags;
        [FieldOffset(121)]
        public byte FoveatedTier;
        [FieldOffset(122)]
        public byte AcousticEnvironment;
        [FieldOffset(123)]
        public VirtualVoiceDspFlags DspFlags;
        [FieldOffset(124)]
        private byte _reserved0;
    }

    /// <summary>
    /// Compact ranking key for cache-line-friendly virtual voice selection.
    /// Sorting this 16-byte stream avoids swapping 160-byte voice payloads.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct VirtualVoiceSortKey
    {
        [FieldOffset(0)]
        public float Weight;
        [FieldOffset(4)]
        public int VoiceIndex;
        [FieldOffset(8)]
        public uint StableKey;
        [FieldOffset(12)]
        public uint Padding;
    }

    /// <summary>
    /// Selected physical channel candidate after virtual voice ranking.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 144)]
    public struct VirtualVoiceSelection
    {
        [FieldOffset(0)]
        public AcousticAup SourceAup;
        [FieldOffset(40)]
        public float3 SourceVelocityMetersPerSecond;
        [FieldOffset(52)]
        public float Volume;
        [FieldOffset(56)]
        public float Pitch;
        [FieldOffset(60)]
        public float DopplerRatio;
        [FieldOffset(64)]
        public float Attenuation;
        [FieldOffset(68)]
        public float Weight;
        [FieldOffset(72)]
        public float DistanceSq;
        [FieldOffset(76)]
        public float EffectiveVolume;
        [FieldOffset(80)]
        public float SabineRt60Seconds;
        [FieldOffset(84)]
        public float LowPassCutoffHz;
        [FieldOffset(88)]
        public float DelaySeconds;
        [FieldOffset(92)]
        public uint EventID;
        [FieldOffset(96)]
        public uint ClipHash;
        [FieldOffset(100)]
        public uint StableKey;
        [FieldOffset(104)]
        public uint SourceEntityID;
        [FieldOffset(108)]
        public int StationaryCacheKey;
        [FieldOffset(112)]
        public VirtualVoicePortalFlags PortalFlags;
        [FieldOffset(113)]
        public byte FoveatedTier;
        [FieldOffset(114)]
        public byte AcousticEnvironment;
        [FieldOffset(115)]
        public VirtualVoiceDspFlags DspFlags;
        [FieldOffset(116)]
        private byte _reserved0;
    }

    /// <summary>
    /// Last virtual voice sort pass counters.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VirtualVoiceStatistics
    {
        [FieldOffset(0)]
        public int Frame;
        [FieldOffset(4)]
        public int TotalVoices;
        [FieldOffset(8)]
        public int AudibleVoices;
        [FieldOffset(12)]
        public int CulledVoices;
        [FieldOffset(16)]
        public int ActivePhysicalVoices;
        [FieldOffset(20)]
        public int PhysicalVoiceLimit;
        [FieldOffset(24)]
        public int StolenVoices;
        [FieldOffset(28)]
        public int DroppedVoices;
        [FieldOffset(32)]
        public int OccludedVoices;
        [FieldOffset(36)]
        public int DelayedVoices;
        [FieldOffset(40)]
        public float SortTimeMs;
        [FieldOffset(44)]
        public float LoudestWeight;
        [FieldOffset(48)]
        public float AverageRt60Seconds;
        [FieldOffset(52)]
        public float AverageLowPassHertz;
        [FieldOffset(56)]
        public float MaximumDelaySeconds;
        [FieldOffset(60)]
        public float AcousticOcclusionTimeMs;
    }

    /// <summary>
    /// Fixed-size black-box entry for the last 300 SDF acoustic occlusion frames.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AcousticTelemetryEntry
    {
        [FieldOffset(0)]
        public int Frame;
        [FieldOffset(4)]
        public uint StateHash;
        [FieldOffset(8)]
        public float LoudestWeight;
        [FieldOffset(12)]
        public float SortTimeMs;
        [FieldOffset(16)]
        public float AverageRt60Seconds;
        [FieldOffset(20)]
        public float AverageLowPassHertz;
        [FieldOffset(24)]
        public float MaximumDelaySeconds;
        [FieldOffset(28)]
        public float AcousticOcclusionTimeMs;
        [FieldOffset(32)]
        public ushort TotalVoices;
        [FieldOffset(34)]
        public ushort AudibleVoices;
        [FieldOffset(36)]
        public ushort CulledVoices;
        [FieldOffset(38)]
        public ushort ActiveVoices;
        [FieldOffset(40)]
        public ushort PhysicalVoiceLimit;
        [FieldOffset(42)]
        public ushort StolenVoices;
        [FieldOffset(44)]
        public ushort DroppedVoices;
        [FieldOffset(46)]
        public ushort Flags;
        [FieldOffset(48)]
        public ushort OccludedVoices;
        [FieldOffset(50)]
        public ushort DelayedVoices;
        [FieldOffset(52)]
        private uint _reserved1;
        [FieldOffset(56)]
        private uint _reserved2;
        [FieldOffset(60)]
        private uint _reserved3;
    }

    /// <summary>
    /// Fixed-size compatibility black-box entry for virtual voice consumers.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VirtualVoiceTelemetryEntry
    {
        [FieldOffset(0)]
        public int Frame;
        [FieldOffset(4)]
        public uint StateHash;
        [FieldOffset(8)]
        public float LoudestWeight;
        [FieldOffset(12)]
        public float SortTimeMs;
        [FieldOffset(16)]
        public float AverageRt60Seconds;
        [FieldOffset(20)]
        public float AverageLowPassHertz;
        [FieldOffset(24)]
        public float MaximumDelaySeconds;
        [FieldOffset(28)]
        public float AcousticOcclusionTimeMs;
        [FieldOffset(32)]
        public ushort TotalVoices;
        [FieldOffset(34)]
        public ushort AudibleVoices;
        [FieldOffset(36)]
        public ushort CulledVoices;
        [FieldOffset(38)]
        public ushort ActiveVoices;
        [FieldOffset(40)]
        public ushort PhysicalVoiceLimit;
        [FieldOffset(42)]
        public ushort StolenVoices;
        [FieldOffset(44)]
        public ushort DroppedVoices;
        [FieldOffset(46)]
        public ushort Flags;
        [FieldOffset(48)]
        public ushort OccludedVoices;
        [FieldOffset(50)]
        public ushort DelayedVoices;
        [FieldOffset(52)]
        private uint _reserved1;
        [FieldOffset(56)]
        private uint _reserved2;
        [FieldOffset(60)]
        private uint _reserved3;
    }

    /// <summary>
    /// Vault-resident runtime knobs for the Sabine/Doppler virtual voice job.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VirtualVoiceTuningSnapshot
    {
        [FieldOffset(0)]
        public float SoundSpeedMetersPerSecond;
        [FieldOffset(4)]
        public float GlobalOcclusionPenalty;
        [FieldOffset(8)]
        public float OccludedLowPassHertz;
        [FieldOffset(12)]
        public float SabineDecayScale;
        [FieldOffset(16)]
        public int MaxHydratedVoices;
        [FieldOffset(20)]
        public byte DisableSdfOcclusion;
        [FieldOffset(21)]
        private byte _reserved0;
        [FieldOffset(22)]
        private ushort _reserved1;
        [FieldOffset(24)]
        private uint _reserved2;

        public static VirtualVoiceTuningSnapshot CreateDefault()
        {
            return new VirtualVoiceTuningSnapshot
            {
                SoundSpeedMetersPerSecond = VirtualVoiceUtility.DelaySpeedMetersPerSecond,
                GlobalOcclusionPenalty = VirtualVoiceUtility.DearLieOccludedGain,
                OccludedLowPassHertz = VirtualVoiceUtility.OccludedLowPassHertz,
                SabineDecayScale = 1f,
                MaxHydratedVoices = VirtualVoiceUtility.MaxPhysicalVoiceCount,
                DisableSdfOcclusion = 0
            };
        }

        public static VirtualVoiceTuningSnapshot Sanitize(in VirtualVoiceTuningSnapshot tuning)
        {
            return new VirtualVoiceTuningSnapshot
            {
                SoundSpeedMetersPerSecond = math.clamp(
                    VirtualVoiceUtility.SanitizeFinite(tuning.SoundSpeedMetersPerSecond, VirtualVoiceUtility.DelaySpeedMetersPerSecond),
                    250f,
                    2000f),
                GlobalOcclusionPenalty = math.clamp(
                    VirtualVoiceUtility.SanitizeFinite(tuning.GlobalOcclusionPenalty, VirtualVoiceUtility.DearLieOccludedGain),
                    0.03162278f,
                    1f),
                OccludedLowPassHertz = math.clamp(
                    VirtualVoiceUtility.SanitizeFinite(tuning.OccludedLowPassHertz, VirtualVoiceUtility.OccludedLowPassHertz),
                    80f,
                    VirtualVoiceUtility.OpenLowPassHertz),
                SabineDecayScale = math.clamp(
                    VirtualVoiceUtility.SanitizeFinite(tuning.SabineDecayScale, 1f),
                    0.1f,
                    4f),
                MaxHydratedVoices = math.clamp(
                    tuning.MaxHydratedVoices,
                    1,
                    VirtualVoiceUtility.MaxPhysicalVoiceCount),
                DisableSdfOcclusion = tuning.DisableSdfOcclusion != 0 ? (byte)1 : (byte)0
            };
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AudioProfileCsvRow
    {
        [FieldOffset(0)]
        public uint SoundHash;
        [FieldOffset(4)]
        public uint KeyHash;
        [FieldOffset(8)]
        public float Value;
        [FieldOffset(12)]
        public byte Kind;
        [FieldOffset(13)]
        private byte _reserved0;
        [FieldOffset(14)]
        private ushort _reserved1;
        [FieldOffset(16)]
        private uint _reserved2;
        [FieldOffset(20)]
        private uint _reserved3;
    }

    /// <summary>
    /// DSP echo tap payload produced from virtual acoustic selections and bridged to sensory systems.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 144)]
    public partial struct AcousticEchoTap
    {
        [FieldOffset(0)]
        public AcousticAup SourceAup;
        [FieldOffset(40)]
        public AcousticAup ListenerAup;
        [FieldOffset(80)]
        public float3 Position;
        [FieldOffset(92)]
        public float Magnitude;
        [FieldOffset(96)]
        public float Volume01;
        [FieldOffset(100)]
        public float DelaySeconds;
        [FieldOffset(104)]
        public float LowPassCutoffHz;
        [FieldOffset(108)]
        public float Rt60Seconds;
        [FieldOffset(112)]
        public uint SoundHash;
        [FieldOffset(116)]
        public uint SourceId;
        [FieldOffset(120)]
        public uint ClipHash;
        [FieldOffset(124)]
        public VirtualVoiceDspFlags Flags;
        [FieldOffset(125)]
        public byte QualityTier;
        [FieldOffset(126)]
        private ushort _reserved0;
        [FieldOffset(128)]
        private uint _reserved1;
        [FieldOffset(132)]
        private uint _reserved2;
        [FieldOffset(136)]
        private uint _reserved3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct MockAcousticEmitterSignal
    {
        [FieldOffset(0)]
        public AcousticAup SourceAup;
        [FieldOffset(40)]
        public float3 SourceVelocityMetersPerSecond;
        [FieldOffset(52)]
        public uint EventID;
        [FieldOffset(56)]
        public uint ClipHash;
        [FieldOffset(60)]
        public uint SourceEntityID;
        [FieldOffset(64)]
        public float Volume;
        [FieldOffset(68)]
        public float Pitch;
        [FieldOffset(72)]
        public float Importance;
        [FieldOffset(76)]
        public VirtualVoiceDspFlags Flags;
        [FieldOffset(77)]
        public byte AcousticEnvironment;
        [FieldOffset(78)]
        private ushort _reserved0;
        [FieldOffset(80)]
        private uint _reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MockPlayerInsideSubSignal
    {
        [FieldOffset(0)]
        public uint SourceEntityID;
        [FieldOffset(4)]
        public float Interior01;
        [FieldOffset(8)]
        public float HullLowPassHertz;
        [FieldOffset(12)]
        public uint Frame;
        [FieldOffset(16)]
        public byte Active;
        [FieldOffset(17)]
        private byte _reserved0;
        [FieldOffset(18)]
        private ushort _reserved1;
        [FieldOffset(20)]
        private uint _reserved2;
        [FieldOffset(24)]
        private uint _reserved3;
    }

    /// <summary>
    /// Cheap deterministic SDF stand-in. Negative sample means "solid between listener and source".
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MockSDFSampler
    {
        [FieldOffset(0)]
        public float3 Center;
        [FieldOffset(12)]
        public float3 HalfExtents;
        [FieldOffset(24)]
        public float WallPlaneY;
        [FieldOffset(28)]
        public float WallThickness;
        [FieldOffset(32)]
        public byte Enabled;
        [FieldOffset(33)]
        public byte UseBox;
        [FieldOffset(34)]
        private ushort _reserved0;
        [FieldOffset(36)]
        private uint _reserved1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Sample(float3 position)
        {
            if (Enabled == 0)
                return 1f;

            if (UseBox != 0)
            {
                float3 q = math.abs(position - Center) - math.max(HalfExtents, new float3(0.001f));
                float outside = VirtualVoiceUtility.FastLength(math.max(q, float3.zero));
                float inside = math.min(math.max(q.x, math.max(q.y, q.z)), 0f);
                return outside + inside;
            }

            float thickness = math.max(0.001f, WallThickness);
            return math.abs(position.y - WallPlaneY) - thickness;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public partial struct MockTerrainSampler
    {
        [FieldOffset(0)]
        public MockSDFSampler SdfSampler;
        [FieldOffset(64)]
        public float ReverbVolumeCubicMeters;
        [FieldOffset(68)]
        public byte AcousticEnvironment;
        [FieldOffset(69)]
        private byte _reserved0;
        [FieldOffset(70)]
        private ushort _reserved1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SampleDistance(float3 position)
        {
            return SdfSampler.Sample(position);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ResolveRt60()
        {
            return VirtualVoiceUtility.ComputeSabineRt60(ReverbVolumeCubicMeters, AcousticEnvironment);
        }
    }

    public static class VirtualVoiceUtility
    {
        public const int MaxVirtualVoiceCount = 1000;
        public const int MaxPhysicalVoiceCount = 64;
        public const int LowTierPhysicalVoiceCount = 12;
        public const int FoveatedTierFrozen = 2;
        public const float MinimumAudibleEnergy = 0.01f;
        public const float MinimumDopplerRatio = 0.1f;
        public const float MaximumDopplerRatio = 3f;
        public const float DopplerSpeedMetersPerSecond = HectonPhysicsContract.SoundSpeedWaterMetersPerSecondConst;
        public const float DelaySpeedMetersPerSecond = 1500f;
        public const float DelaySpeedMetersPerSecondInv = 0.0006666667f;
        public const float DearLieOccludedGain = 0.25118864f;
        public const float OpenLowPassHertz = 22000f;
        public const float OccludedLowPassHertz = 900f;
        public const float HullLowPassHertz = 1200f;
        public const float SabineEquationConstant = 0.161f;
        public const float SabineMinimumRt60Seconds = 0.05f;
        public const float SabineMaximumRt60Seconds = 12f;
        public const float AbyssLowPassHertz = 800f;
        public const float MaximumDepthLowPassMeters = 6000f;
        public const float MaximumUnderwaterItdSeconds = 0.00018f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComputeStableKey(uint eventID, uint clipHash, uint sourceEntityID, int stationaryCacheKey, in AcousticAup sourceAup)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ eventID) * 16777619u;
                hash = (hash ^ clipHash) * 16777619u;
                hash = (hash ^ sourceEntityID) * 16777619u;
                hash = (hash ^ (uint)stationaryCacheKey) * 16777619u;
                hash = (hash ^ (uint)sourceAup.GridX) * 16777619u;
                hash = (hash ^ (uint)(sourceAup.GridX >> 32)) * 16777619u;
                hash = (hash ^ (uint)sourceAup.GridY) * 16777619u;
                hash = (hash ^ (uint)(sourceAup.GridY >> 32)) * 16777619u;
                hash = (hash ^ (uint)sourceAup.GridZ) * 16777619u;
                hash = (hash ^ (uint)(sourceAup.GridZ >> 32)) * 16777619u;
                hash = (hash ^ (uint)math.round(sourceAup.Local.x)) * 16777619u;
                hash = (hash ^ (uint)math.round(sourceAup.Local.y)) * 16777619u;
                hash = (hash ^ (uint)math.round(sourceAup.Local.z)) * 16777619u;
                return hash != 0u ? hash : 1u;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComputeStableKey(uint eventID, uint clipHash, int stationaryCacheKey, in AcousticAup sourceAup)
        {
            return ComputeStableKey(eventID, clipHash, 0u, stationaryCacheKey, in sourceAup);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ComputeDopplerRatio(float3 listenerToSource, float3 sourceVelocity, float3 listenerVelocity, float authoredRatio)
        {
            return ComputeDopplerRatio(
                listenerToSource,
                sourceVelocity,
                listenerVelocity,
                authoredRatio,
                DopplerSpeedMetersPerSecond);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ComputeDopplerRatio(
            float3 listenerToSource,
            float3 sourceVelocity,
            float3 listenerVelocity,
            float authoredRatio,
            float soundSpeedMetersPerSecond)
        {
            float distanceSq = math.lengthsq(listenerToSource);
            float baseRatio = math.clamp(SanitizeFinite(authoredRatio, 1f), MinimumDopplerRatio, MaximumDopplerRatio);
            if (distanceSq <= 0.0001f || !math.isfinite(distanceSq))
                return baseRatio;

            float soundSpeed = math.clamp(SanitizeFinite(soundSpeedMetersPerSecond, DopplerSpeedMetersPerSecond), 250f, 2000f);
            float3 direction = listenerToSource * math.rsqrt(distanceSq);
            float listenerAlong = math.clamp(math.dot(listenerVelocity, direction), -soundSpeed * 0.9f, soundSpeed * 0.9f);
            float sourceAlong = math.clamp(math.dot(sourceVelocity, direction), -soundSpeed * 0.9f, soundSpeed * 0.9f);
            float numerator = soundSpeed + listenerAlong;
            float denominator = math.max(soundSpeed + sourceAlong, soundSpeed * 0.1f);
            return math.clamp(baseRatio * numerator * math.rcp(denominator), MinimumDopplerRatio, MaximumDopplerRatio);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ComputeDelaySeconds(float distanceSq)
        {
            return ComputeDelaySeconds(distanceSq, DelaySpeedMetersPerSecond);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ComputeDelaySeconds(float distanceSq, float speedMetersPerSecond)
        {
            if (distanceSq <= 0.0001f || !math.isfinite(distanceSq))
                return 0f;

            float speed = math.max(1f, SanitizeFinite(speedMetersPerSecond, DelaySpeedMetersPerSecond));
            return distanceSq * math.rsqrt(distanceSq) * math.rcp(speed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ComputeSabineRt60(float roomVolumeCubicMeters, byte acousticEnvironment)
        {
            float volume = math.max(0.01f, SanitizeFinite(roomVolumeCubicMeters, 0f));
            float side = ApproximateCubeRoot(volume);
            float surfaceArea = math.max(0.5f, 6f * side * side);
            float absorption = ResolveAbsorption(acousticEnvironment);
            return math.clamp(
                SabineEquationConstant * volume * math.rcp(surfaceArea * absorption),
                SabineMinimumRt60Seconds,
                SabineMaximumRt60Seconds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ComputeSabineRt60FromClearance(float clearanceMeters, float absorption01, float qualityWeight)
        {
            float clearance = math.clamp(SanitizeFinite(math.abs(clearanceMeters), 1f), 0.35f, 96f);
            float quality = math.saturate(SanitizeFinite(qualityWeight, 0f));
            float sideMeters = math.clamp(clearance * math.lerp(3f, 9f, quality), 1.5f, 160f);
            float volume = sideMeters * sideMeters * sideMeters;
            float surfaceArea = math.max(0.5f, 6f * sideMeters * sideMeters);
            float absorption = math.clamp(SanitizeFinite(absorption01, 0.35f), 0.03f, 1f);
            return math.clamp(
                SabineEquationConstant * volume * math.rcp(math.max(surfaceArea * absorption, 0.0001f)),
                SabineMinimumRt60Seconds,
                SabineMaximumRt60Seconds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ResolveDearLieOcclusion(float3 listenerToSource, in MockSDFSampler sampler, int disableSdfOcclusion)
        {
            if (disableSdfOcclusion != 0 || sampler.Enabled == 0)
                return false;

            float signedDistance = sampler.Sample(listenerToSource * 0.5f);
            return math.isfinite(signedDistance) && signedDistance < 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveContinuousVoiceBudget(float globalQualityWeight)
        {
            float quality = math.saturate(SanitizeFinite(globalQualityWeight, 0f));
            return math.clamp(
                (int)math.lerp((float)LowTierPhysicalVoiceCount, (float)MaxPhysicalVoiceCount, quality),
                LowTierPhysicalVoiceCount,
                MaxPhysicalVoiceCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveSdfTapCount(float globalQualityWeight)
        {
            float quality = math.saturate(SanitizeFinite(globalQualityWeight, 0f));
            float curve = quality * quality * (3f - 2f * quality);
            return math.clamp((int)math.round(math.lerp(1f, 8f, curve)), 1, 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveDepthLowPassHertz(float depthMeters, float globalQualityWeight)
        {
            float depth01 = math.saturate(SanitizeFinite(depthMeters, 0f) * math.rcp(MaximumDepthLowPassMeters));
            float curvedDepth = depth01 * depth01 * (3f - 2f * depth01);
            float quality = math.saturate(SanitizeFinite(globalQualityWeight, 0f));
            float floor = math.lerp(AbyssLowPassHertz, 1400f, quality);
            return math.clamp(math.lerp(OpenLowPassHertz, floor, curvedDepth), floor, OpenLowPassHertz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ComputeUnderwaterItdSeconds(float3 listenerToSource, float3 listenerRight)
        {
            float distanceSq = math.lengthsq(listenerToSource);
            if (distanceSq <= 0.0001f || !math.isfinite(distanceSq))
                return 0f;

            float3 direction = listenerToSource * math.rsqrt(math.max(distanceSq, 0.0001f));
            float side = math.clamp(math.dot(direction, listenerRight), -1f, 1f);
            return side * MaximumUnderwaterItdSeconds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastLength(float3 value)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > 0.000001f ? lengthSq * math.rsqrt(lengthSq) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ApproximateCubeRoot(float value)
        {
            uint bits = math.asuint(math.max(value, 0.000001f));
            bits = bits / 3u + (uint)HectonPhysicsContract.CubeRootMagicBias;
            float estimate = math.asfloat(bits);
            float denominator = math.max(estimate * estimate, 0.000001f);
            return math.max(0.01f, (2f * estimate + value * math.rcp(denominator)) * HectonPhysicsContract.CubeRootNewtonOneThird);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveAbsorption(byte acousticEnvironment)
        {
            switch (acousticEnvironment)
            {
                case 1:
                    return 0.18f; // steel hull
                case 2:
                    return 0.32f; // rock
                case 3:
                    return 0.44f; // coral
                case 4:
                    return 0.72f; // open water
                default:
                    return 0.35f;
            }
        }
    }

    public static class VirtualVoiceProfileCsvParser
    {
        private const byte RowKindGlobalTuning = 1;
        private const byte RowKindSoundOverride = 2;
        private const uint SpeedOfSoundHash = 0xFC39038Cu;
        private const uint SoundSpeedHash = 0x6695CCACu;
        private const uint GlobalOcclusionPenaltyHash = 0x05CDE0CAu;
        private const uint OcclusionGainHash = 0xDCB4E1A0u;
        private const uint OccludedLowPassHash = 0x7FF4AE83u;
        private const uint LowPassHash = 0x2CBC6DB3u;
        private const uint SabineDecayTimesHash = 0x739374A1u;
        private const uint SabineDecayScaleHash = 0x52D55625u;
        private const uint MaxHydratedVoicesHash = 0xFD9F8B83u;
        private const uint PhysicalVoiceLimitHash = 0x48781CB9u;
        private const uint DisableSdfOcclusionHash = 0x04D10E79u;

        public static bool TryReadTuning(ReadOnlySpan<char> csv, ref VirtualVoiceTuningSnapshot tuning)
        {
            bool any = false;
            int cursor = 0;
            while (TryReadLine(csv, ref cursor, out ReadOnlySpan<char> line))
            {
                line = Trim(line);
                if (line.Length <= 0 || line[0] == '#')
                    continue;

                if (!TryReadKeyValue(line, out ReadOnlySpan<char> key, out ReadOnlySpan<char> valueSpan))
                    continue;

                if (!TryParseFloat(valueSpan, out float value))
                    continue;

                uint keyHash = HashLowerAscii(key);
                if (keyHash == SpeedOfSoundHash || keyHash == SoundSpeedHash)
                {
                    tuning.SoundSpeedMetersPerSecond = value;
                    any = true;
                }
                else if (keyHash == GlobalOcclusionPenaltyHash || keyHash == OcclusionGainHash)
                {
                    tuning.GlobalOcclusionPenalty = value;
                    any = true;
                }
                else if (keyHash == OccludedLowPassHash || keyHash == LowPassHash)
                {
                    tuning.OccludedLowPassHertz = value;
                    any = true;
                }
                else if (keyHash == SabineDecayTimesHash || keyHash == SabineDecayScaleHash)
                {
                    tuning.SabineDecayScale = value;
                    any = true;
                }
                else if (keyHash == MaxHydratedVoicesHash || keyHash == PhysicalVoiceLimitHash)
                {
                    tuning.MaxHydratedVoices = (int)math.round(value);
                    any = true;
                }
                else if (keyHash == DisableSdfOcclusionHash)
                {
                    tuning.DisableSdfOcclusion = value > 0.5f ? (byte)1 : (byte)0;
                    any = true;
                }
            }

            if (any)
                tuning = VirtualVoiceTuningSnapshot.Sanitize(in tuning);
            return any;
        }

        public static int ParseRows(ReadOnlySpan<char> csv, NativeArray<AudioProfileCsvRow> rows)
        {
            if (!rows.IsCreated)
                return 0;

            int count = 0;
            int cursor = 0;
            while (count < rows.Length && TryReadLine(csv, ref cursor, out ReadOnlySpan<char> line))
            {
                line = Trim(line);
                if (line.Length <= 0 || line[0] == '#')
                    continue;

                if (!TryReadProfileRow(line, out AudioProfileCsvRow row))
                    continue;

                rows[count++] = row;
            }

            return count;
        }

        public static bool TryReadTuning(ReadOnlySpan<byte> csv, ref VirtualVoiceTuningSnapshot tuning)
        {
            bool any = false;
            int cursor = 0;
            while (TryReadLine(csv, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = Trim(line);
                if (line.Length <= 0 || line[0] == (byte)'#')
                    continue;

                if (!TryReadKeyValue(line, out ReadOnlySpan<byte> key, out ReadOnlySpan<byte> valueSpan))
                    continue;

                if (!TryParseFloat(valueSpan, out float value))
                    continue;

                uint keyHash = HashLowerAscii(key);
                if (keyHash == SpeedOfSoundHash || keyHash == SoundSpeedHash)
                {
                    tuning.SoundSpeedMetersPerSecond = value;
                    any = true;
                }
                else if (keyHash == GlobalOcclusionPenaltyHash || keyHash == OcclusionGainHash)
                {
                    tuning.GlobalOcclusionPenalty = value;
                    any = true;
                }
                else if (keyHash == OccludedLowPassHash || keyHash == LowPassHash)
                {
                    tuning.OccludedLowPassHertz = value;
                    any = true;
                }
                else if (keyHash == SabineDecayTimesHash || keyHash == SabineDecayScaleHash)
                {
                    tuning.SabineDecayScale = value;
                    any = true;
                }
                else if (keyHash == MaxHydratedVoicesHash || keyHash == PhysicalVoiceLimitHash)
                {
                    tuning.MaxHydratedVoices = (int)math.round(value);
                    any = true;
                }
                else if (keyHash == DisableSdfOcclusionHash)
                {
                    tuning.DisableSdfOcclusion = value > 0.5f ? (byte)1 : (byte)0;
                    any = true;
                }
            }

            if (any)
                tuning = VirtualVoiceTuningSnapshot.Sanitize(in tuning);
            return any;
        }

        public static int ParseRows(ReadOnlySpan<byte> csv, NativeArray<AudioProfileCsvRow> rows)
        {
            if (!rows.IsCreated)
                return 0;

            int count = 0;
            int cursor = 0;
            while (count < rows.Length && TryReadLine(csv, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = Trim(line);
                if (line.Length <= 0 || line[0] == (byte)'#')
                    continue;

                if (!TryReadProfileRow(line, out AudioProfileCsvRow row))
                    continue;

                rows[count++] = row;
            }

            return count;
        }

        public static int ParseMaterialRows(ReadOnlySpan<byte> csv, NativeArray<AcousticMaterialCoefficientDTO> rows)
        {
            if (!rows.IsCreated)
                return 0;

            int count = 0;
            int cursor = 0;
            while (count < rows.Length && TryReadLine(csv, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = Trim(line);
                if (line.Length <= 0 || line[0] == (byte)'#')
                    continue;

                if (!TryReadMaterialRow(line, out AcousticMaterialCoefficientDTO row))
                    continue;

                rows[count++] = row;
            }

            return count;
        }

        public static int ParseMaterialRows(ReadOnlySpan<byte> csv, NativeParallelHashMap<uint, AcousticMaterialCoefficientDTO> rows)
        {
            if (!rows.IsCreated)
                return 0;

            rows.Clear();
            int count = 0;
            int cursor = 0;
            while (TryReadLine(csv, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = Trim(line);
                if (line.Length <= 0 || line[0] == (byte)'#')
                    continue;

                if (!TryReadMaterialRow(line, out AcousticMaterialCoefficientDTO row))
                    continue;

                rows[row.MaterialHash] = row;
                count++;
            }

            return count;
        }

        public static int GenerateEmergencyMockAcoustics(NativeArray<AcousticMaterialCoefficientDTO> rows)
        {
            if (!rows.IsCreated || rows.Length <= 0)
                return 0;

            int count = math.min(rows.Length, 3);
            if (count > 0)
            {
                rows[0] = new AcousticMaterialCoefficientDTO
                {
                    MaterialHash = 0x3A1B4AB4u,
                    Absorption01 = 0.32f,
                    Scatter01 = 0.55f,
                    Density01 = 0.85f,
                    LowPassHertz = 2100f,
                    Flags = 1u
                };
            }

            if (count > 1)
            {
                rows[1] = new AcousticMaterialCoefficientDTO
                {
                    MaterialHash = 0xD756AEDCu,
                    Absorption01 = 0.18f,
                    Scatter01 = 0.28f,
                    Density01 = 1f,
                    LowPassHertz = 3400f,
                    Flags = 1u
                };
            }

            if (count > 2)
            {
                rows[2] = new AcousticMaterialCoefficientDTO
                {
                    MaterialHash = 0x02FC484Du,
                    Absorption01 = 0.62f,
                    Scatter01 = 0.75f,
                    Density01 = 0.45f,
                    LowPassHertz = 1200f,
                    Flags = 1u
                };
            }

            return count;
        }

        public static int GenerateEmergencyMockAcoustics(NativeParallelHashMap<uint, AcousticMaterialCoefficientDTO> rows)
        {
            if (!rows.IsCreated)
                return 0;

            rows.Clear();
            int capacity = rows.Capacity;
            if (capacity <= 0)
                return 0;

            AcousticMaterialCoefficientDTO rock = new AcousticMaterialCoefficientDTO
            {
                MaterialHash = 0x3A1B4AB4u,
                Absorption01 = 0.32f,
                Scatter01 = 0.55f,
                Density01 = 0.85f,
                LowPassHertz = 2100f,
                Flags = 1u
            };
            AcousticMaterialCoefficientDTO metal = new AcousticMaterialCoefficientDTO
            {
                MaterialHash = 0xD756AEDCu,
                Absorption01 = 0.18f,
                Scatter01 = 0.28f,
                Density01 = 1f,
                LowPassHertz = 3400f,
                Flags = 1u
            };
            AcousticMaterialCoefficientDTO flesh = new AcousticMaterialCoefficientDTO
            {
                MaterialHash = 0x02FC484Du,
                Absorption01 = 0.62f,
                Scatter01 = 0.75f,
                Density01 = 0.45f,
                LowPassHertz = 1200f,
                Flags = 1u
            };

            int written = 0;
            if (written < capacity && rows.TryAdd(rock.MaterialHash, rock))
                written++;
            if (written < capacity && rows.TryAdd(metal.MaterialHash, metal))
                written++;
            if (written < capacity && rows.TryAdd(flesh.MaterialHash, flesh))
                written++;

            return written;
        }

        private static bool TryReadProfileRow(ReadOnlySpan<char> line, out AudioProfileCsvRow row)
        {
            row = default;
            int firstDelimiter = IndexOfDelimiter(line, 0);
            if (firstDelimiter < 0)
                return false;

            ReadOnlySpan<char> first = Trim(line.Slice(0, firstDelimiter));
            ReadOnlySpan<char> remainder = line.Slice(firstDelimiter + 1);
            int secondDelimiter = IndexOfDelimiter(remainder, 0);
            if (secondDelimiter < 0)
            {
                if (!TryParseFloat(Trim(remainder), out float value))
                    return false;

                row = new AudioProfileCsvRow
                {
                    SoundHash = 0u,
                    KeyHash = HashLowerAscii(first),
                    Value = value,
                    Kind = RowKindGlobalTuning
                };
                return true;
            }

            ReadOnlySpan<char> key = Trim(remainder.Slice(0, secondDelimiter));
            ReadOnlySpan<char> valueSpan = Trim(remainder.Slice(secondDelimiter + 1));
            if (!TryParseUInt(first, out uint soundHash) || !TryParseFloat(valueSpan, out float parsedValue))
                return false;

            row = new AudioProfileCsvRow
            {
                SoundHash = soundHash,
                KeyHash = HashLowerAscii(key),
                Value = parsedValue,
                Kind = RowKindSoundOverride
            };
            return true;
        }

        private static bool TryReadProfileRow(ReadOnlySpan<byte> line, out AudioProfileCsvRow row)
        {
            row = default;
            int firstDelimiter = IndexOfDelimiter(line, 0);
            if (firstDelimiter < 0)
                return false;

            ReadOnlySpan<byte> first = Trim(line.Slice(0, firstDelimiter));
            ReadOnlySpan<byte> remainder = line.Slice(firstDelimiter + 1);
            int secondDelimiter = IndexOfDelimiter(remainder, 0);
            if (secondDelimiter < 0)
            {
                if (!TryParseFloat(Trim(remainder), out float value))
                    return false;

                row = new AudioProfileCsvRow
                {
                    SoundHash = 0u,
                    KeyHash = HashLowerAscii(first),
                    Value = value,
                    Kind = RowKindGlobalTuning
                };
                return true;
            }

            ReadOnlySpan<byte> key = Trim(remainder.Slice(0, secondDelimiter));
            ReadOnlySpan<byte> valueSpan = Trim(remainder.Slice(secondDelimiter + 1));
            if (!TryParseUInt(first, out uint soundHash) || !TryParseFloat(valueSpan, out float parsedValue))
                return false;

            row = new AudioProfileCsvRow
            {
                SoundHash = soundHash,
                KeyHash = HashLowerAscii(key),
                Value = parsedValue,
                Kind = RowKindSoundOverride
            };
            return true;
        }

        private static bool TryReadMaterialRow(ReadOnlySpan<byte> line, out AcousticMaterialCoefficientDTO row)
        {
            row = default;
            int firstDelimiter = IndexOfDelimiter(line, 0);
            if (firstDelimiter < 0)
                return false;

            ReadOnlySpan<byte> material = Trim(line.Slice(0, firstDelimiter));
            ReadOnlySpan<byte> rest = line.Slice(firstDelimiter + 1);
            int secondDelimiter = IndexOfDelimiter(rest, 0);
            if (secondDelimiter < 0)
                return false;

            ReadOnlySpan<byte> absorptionSpan = Trim(rest.Slice(0, secondDelimiter));
            rest = rest.Slice(secondDelimiter + 1);
            int thirdDelimiter = IndexOfDelimiter(rest, 0);
            if (thirdDelimiter < 0)
                return false;

            ReadOnlySpan<byte> scatterSpan = Trim(rest.Slice(0, thirdDelimiter));
            rest = rest.Slice(thirdDelimiter + 1);
            int fourthDelimiter = IndexOfDelimiter(rest, 0);
            ReadOnlySpan<byte> densitySpan = fourthDelimiter >= 0
                ? Trim(rest.Slice(0, fourthDelimiter))
                : Trim(rest);
            ReadOnlySpan<byte> lowPassSpan = fourthDelimiter >= 0
                ? Trim(rest.Slice(fourthDelimiter + 1))
                : ReadOnlySpan<byte>.Empty;

            if (!TryParseFloat(absorptionSpan, out float absorption) ||
                !TryParseFloat(scatterSpan, out float scatter) ||
                !TryParseFloat(densitySpan, out float density))
            {
                return false;
            }

            float lowPass = VirtualVoiceUtility.OpenLowPassHertz;
            if (lowPassSpan.Length > 0)
                TryParseFloat(lowPassSpan, out lowPass);

            row = new AcousticMaterialCoefficientDTO
            {
                MaterialHash = HashLowerAscii(material),
                Absorption01 = math.saturate(absorption),
                Scatter01 = math.saturate(scatter),
                Density01 = math.saturate(density),
                LowPassHertz = math.clamp(lowPass, 80f, VirtualVoiceUtility.OpenLowPassHertz),
                Flags = 0u
            };
            return true;
        }

        private static bool TryReadLine(ReadOnlySpan<char> text, ref int cursor, out ReadOnlySpan<char> line)
        {
            line = default;
            if (cursor >= text.Length)
                return false;

            int start = cursor;
            while (cursor < text.Length && text[cursor] != '\n' && text[cursor] != '\r')
                cursor++;

            int end = cursor;
            while (cursor < text.Length && (text[cursor] == '\n' || text[cursor] == '\r'))
                cursor++;

            line = text.Slice(start, end - start);
            return true;
        }

        private static bool TryReadLine(ReadOnlySpan<byte> text, ref int cursor, out ReadOnlySpan<byte> line)
        {
            line = default;
            if (cursor >= text.Length)
                return false;

            int start = cursor;
            while (cursor < text.Length && text[cursor] != (byte)'\n' && text[cursor] != (byte)'\r')
                cursor++;

            int end = cursor;
            while (cursor < text.Length && (text[cursor] == (byte)'\n' || text[cursor] == (byte)'\r'))
                cursor++;

            line = text.Slice(start, end - start);
            return true;
        }

        private static bool TryReadKeyValue(ReadOnlySpan<char> line, out ReadOnlySpan<char> key, out ReadOnlySpan<char> value)
        {
            key = default;
            value = default;
            int delimiter = IndexOfDelimiter(line, 0);
            if (delimiter <= 0 || delimiter >= line.Length - 1)
                return false;

            key = Trim(line.Slice(0, delimiter));
            value = Trim(line.Slice(delimiter + 1));
            return key.Length > 0 && value.Length > 0;
        }

        private static bool TryReadKeyValue(ReadOnlySpan<byte> line, out ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value)
        {
            key = default;
            value = default;
            int delimiter = IndexOfDelimiter(line, 0);
            if (delimiter <= 0 || delimiter >= line.Length - 1)
                return false;

            key = Trim(line.Slice(0, delimiter));
            value = Trim(line.Slice(delimiter + 1));
            return key.Length > 0 && value.Length > 0;
        }

        private static int IndexOfDelimiter(ReadOnlySpan<char> text, int start)
        {
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ',' || c == '=' || c == ';')
                    return i;
            }

            return -1;
        }

        private static int IndexOfDelimiter(ReadOnlySpan<byte> text, int start)
        {
            for (int i = start; i < text.Length; i++)
            {
                byte c = text[i];
                if (c == (byte)',' || c == (byte)'=' || c == (byte)';')
                    return i;
            }

            return -1;
        }

        private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> text)
        {
            int start = 0;
            int end = text.Length - 1;
            while (start <= end && char.IsWhiteSpace(text[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(text[end]))
                end--;
            return start <= end ? text.Slice(start, end - start + 1) : ReadOnlySpan<char>.Empty;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> text)
        {
            int start = 0;
            int end = text.Length - 1;
            while (start <= end && IsAsciiWhiteSpace(text[start]))
                start++;
            while (end >= start && IsAsciiWhiteSpace(text[end]))
                end--;
            return start <= end ? text.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool TryParseFloat(ReadOnlySpan<char> text, out float value)
        {
            value = 0f;
            text = Trim(text);
            if (text.Length == 0)
                return false;

            int sign = 1;
            int index = 0;
            if (text[0] == '-')
            {
                sign = -1;
                index = 1;
            }
            else if (text[0] == '+')
            {
                index = 1;
            }

            double result = 0d;
            bool any = false;
            while (index < text.Length && text[index] >= '0' && text[index] <= '9')
            {
                result = result * 10d + (text[index] - '0');
                index++;
                any = true;
            }

            if (index < text.Length && text[index] == '.')
            {
                index++;
                double place = 0.1d;
                while (index < text.Length && text[index] >= '0' && text[index] <= '9')
                {
                    result += (text[index] - '0') * place;
                    place *= 0.1d;
                    index++;
                    any = true;
                }
            }

            if (!any || index != text.Length)
                return false;

            value = (float)(result * sign);
            return math.isfinite(value);
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> text, out float value)
        {
            value = 0f;
            text = Trim(text);
            if (text.Length == 0)
                return false;

            int sign = 1;
            int index = 0;
            if (text[0] == (byte)'-')
            {
                sign = -1;
                index = 1;
            }
            else if (text[0] == (byte)'+')
            {
                index = 1;
            }

            double result = 0d;
            bool any = false;
            while (index < text.Length && text[index] >= (byte)'0' && text[index] <= (byte)'9')
            {
                result = result * 10d + (text[index] - (byte)'0');
                index++;
                any = true;
            }

            if (index < text.Length && text[index] == (byte)'.')
            {
                index++;
                double place = 0.1d;
                while (index < text.Length && text[index] >= (byte)'0' && text[index] <= (byte)'9')
                {
                    result += (text[index] - (byte)'0') * place;
                    place *= 0.1d;
                    index++;
                    any = true;
                }
            }

            if (!any || index != text.Length)
                return false;

            value = (float)(result * sign);
            return math.isfinite(value);
        }

        private static bool TryParseUInt(ReadOnlySpan<char> text, out uint value)
        {
            value = 0u;
            text = Trim(text);
            if (text.Length == 0)
                return false;

            int index = 0;
            if (text.Length > 2 && text[0] == '0' && (text[1] == 'x' || text[1] == 'X'))
            {
                index = 2;
                uint hex = 0u;
                for (; index < text.Length; index++)
                {
                    int digit = HexDigit(text[index]);
                    if (digit < 0)
                        return false;
                    hex = (hex << 4) | (uint)digit;
                }

                value = hex;
                return true;
            }

            uint parsed = 0u;
            for (; index < text.Length; index++)
            {
                char c = text[index];
                if (c < '0' || c > '9')
                    return false;
                parsed = parsed * 10u + (uint)(c - '0');
            }

            value = parsed;
            return true;
        }

        private static bool TryParseUInt(ReadOnlySpan<byte> text, out uint value)
        {
            value = 0u;
            text = Trim(text);
            if (text.Length == 0)
                return false;

            int index = 0;
            if (text.Length > 2 && text[0] == (byte)'0' && (text[1] == (byte)'x' || text[1] == (byte)'X'))
            {
                index = 2;
                uint hex = 0u;
                for (; index < text.Length; index++)
                {
                    int digit = HexDigit((char)text[index]);
                    if (digit < 0)
                        return false;
                    hex = (hex << 4) | (uint)digit;
                }

                value = hex;
                return true;
            }

            uint parsed = 0u;
            for (; index < text.Length; index++)
            {
                byte c = text[index];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;
                parsed = parsed * 10u + (uint)(c - (byte)'0');
            }

            value = parsed;
            return true;
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return c - 'a' + 10;
            if (c >= 'A' && c <= 'F')
                return c - 'A' + 10;
            return -1;
        }

        private static uint HashLowerAscii(ReadOnlySpan<char> text)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c >= 'A' && c <= 'Z')
                        c = (char)(c + 32);
                    hash = (hash ^ c) * 16777619u;
                }

                return hash != 0u ? hash : 1u;
            }
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> text)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < text.Length; i++)
                {
                    byte c = text[i];
                    if (c >= (byte)'A' && c <= (byte)'Z')
                        c = (byte)(c + 32);
                    hash = (hash ^ c) * 16777619u;
                }

                return hash != 0u ? hash : 1u;
            }
        }

        private static bool IsAsciiWhiteSpace(byte value)
        {
            return value == (byte)' ' ||
                value == (byte)'\t' ||
                value == (byte)'\r' ||
                value == (byte)'\n';
        }
    }
}
