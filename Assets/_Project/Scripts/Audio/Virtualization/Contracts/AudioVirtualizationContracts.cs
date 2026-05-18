using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Audio.Propagation;
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

    /// <summary>
    /// Exact compact DTO used by external virtual emitters before AUP/grid expansion.
    /// Size: 48 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct VirtualVoiceDTO
    {
        public double3 AupMeters;
        public float Volume;
        public float Pitch;
        public uint ClipHash;
        public uint SourceEntityID;
        public float Importance;
        public uint Padding;
    }

    /// <summary>
    /// Blittable request queued by gameplay audio emitters before physical channel selection.
    /// Explicit size preserves ARM64-friendly stride while embedding the aligned AcousticAup contract.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct VirtualVoiceRequest
    {
        public AcousticAup SourceAup;
        public float3 SourceVelocityMetersPerSecond;
        public float Volume;
        public float Priority;
        public float Pitch;
        public float DopplerRatio;
        public float SabineRt60Seconds;
        public float SabineRoomVolumeCubicMeters;
        public float LowPassCutoffHz;
        public float DelaySeconds;
        public uint EventID;
        public uint ClipHash;
        public uint SourceEntityID;
        public int StationaryCacheKey;
        public AcousticPortalFlags PortalFlags;
        public byte FoveatedTier;
        public byte AcousticEnvironment;
        public VirtualVoiceDspFlags DspFlags;
        private byte _reserved0;
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
            AcousticPortalFlags portalFlags,
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
            AcousticPortalFlags portalFlags,
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
    [StructLayout(LayoutKind.Sequential, Size = 160)]
    public struct VirtualVoice
    {
        public AcousticAup SourceAup;
        public float3 SourceVelocityMetersPerSecond;
        public float Volume;
        public float Priority;
        public float Pitch;
        public float DopplerRatio;
        public float Attenuation;
        public float Weight;
        public float DistanceSq;
        public float EffectiveVolume;
        public float SabineRt60Seconds;
        public float SabineRoomVolumeCubicMeters;
        public float LowPassCutoffHz;
        public float DelaySeconds;
        public uint EventID;
        public uint ClipHash;
        public uint StableKey;
        public uint SourceEntityID;
        public int StationaryCacheKey;
        public AcousticPortalFlags PortalFlags;
        public byte FoveatedTier;
        public byte AcousticEnvironment;
        public VirtualVoiceDspFlags DspFlags;
        private byte _reserved0;
    }

    /// <summary>
    /// Compact ranking key for cache-line-friendly virtual voice selection.
    /// Sorting this 16-byte stream avoids swapping 160-byte voice payloads.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct VirtualVoiceSortKey
    {
        public float Weight;
        public int VoiceIndex;
        public uint StableKey;
        public uint Padding;
    }

    /// <summary>
    /// Selected physical channel candidate after virtual voice ranking.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 144)]
    public struct VirtualVoiceSelection
    {
        public AcousticAup SourceAup;
        public float Volume;
        public float Pitch;
        public float DopplerRatio;
        public float Attenuation;
        public float Weight;
        public float DistanceSq;
        public float EffectiveVolume;
        public float SabineRt60Seconds;
        public float LowPassCutoffHz;
        public float DelaySeconds;
        public uint EventID;
        public uint ClipHash;
        public uint StableKey;
        public uint SourceEntityID;
        public int StationaryCacheKey;
        public AcousticPortalFlags PortalFlags;
        public byte FoveatedTier;
        public byte AcousticEnvironment;
        public VirtualVoiceDspFlags DspFlags;
        private byte _reserved0;
    }

    /// <summary>
    /// Last virtual voice sort pass counters.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct VirtualVoiceStatistics
    {
        public int Frame;
        public int TotalVoices;
        public int AudibleVoices;
        public int CulledVoices;
        public int ActivePhysicalVoices;
        public int PhysicalVoiceLimit;
        public int StolenVoices;
        public int DroppedVoices;
        public int OccludedVoices;
        public int DelayedVoices;
        public float SortTimeMs;
        public float LoudestWeight;
        public float AverageRt60Seconds;
        public float AverageLowPassHertz;
        public float MaximumDelaySeconds;
        private int _reserved0;
    }

    /// <summary>
    /// Fixed-size black-box entry for the last 300 virtual voice frames.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct VirtualVoiceTelemetryEntry
    {
        public int Frame;
        public uint StateHash;
        public float LoudestWeight;
        public float SortTimeMs;
        public float AverageRt60Seconds;
        public float AverageLowPassHertz;
        public float MaximumDelaySeconds;
        public ushort TotalVoices;
        public ushort AudibleVoices;
        public ushort CulledVoices;
        public ushort ActiveVoices;
        public ushort PhysicalVoiceLimit;
        public ushort StolenVoices;
        public ushort DroppedVoices;
        public ushort Flags;
        public ushort OccludedVoices;
        public ushort DelayedVoices;
        private uint _reserved0;
        private uint _reserved1;
        private uint _reserved2;
    }

    /// <summary>
    /// Vault-resident runtime knobs for the Sabine/Doppler virtual voice job.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct VirtualVoiceTuningSnapshot
    {
        public float SoundSpeedMetersPerSecond;
        public float GlobalOcclusionPenalty;
        public float OccludedLowPassHertz;
        public float SabineDecayScale;
        public int MaxHydratedVoices;
        public byte DisableSdfOcclusion;
        private byte _reserved0;
        private ushort _reserved1;
        private uint _reserved2;

        public static VirtualVoiceTuningSnapshot Default
        {
            get
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

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct AudioProfileCsvRow
    {
        public uint SoundHash;
        public uint KeyHash;
        public float Value;
        public byte Kind;
        private byte _reserved0;
        private ushort _reserved1;
        private uint _reserved2;
        private uint _reserved3;
    }

    /// <summary>
    /// DSP echo tap payload produced from virtual acoustic selections and bridged to sensory systems.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 144)]
    public partial struct AcousticEchoTap
    {
        public AcousticAup SourceAup;
        public AcousticAup ListenerAup;
        public float3 Position;
        public float Magnitude;
        public float Volume01;
        public float DelaySeconds;
        public float LowPassCutoffHz;
        public float Rt60Seconds;
        public uint SoundHash;
        public uint SourceId;
        public uint ClipHash;
        public VirtualVoiceDspFlags Flags;
        public byte QualityTier;
        private ushort _reserved0;
        private uint _reserved1;
        private uint _reserved2;
        private uint _reserved3;
    }

    [StructLayout(LayoutKind.Sequential, Size = 96)]
    public struct MockAcousticEmitterSignal
    {
        public AcousticAup SourceAup;
        public float3 SourceVelocityMetersPerSecond;
        public uint EventID;
        public uint ClipHash;
        public uint SourceEntityID;
        public float Volume;
        public float Pitch;
        public float Importance;
        public VirtualVoiceDspFlags Flags;
        public byte AcousticEnvironment;
        private ushort _reserved0;
        private uint _reserved1;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct MockPlayerInsideSubSignal
    {
        public uint SourceEntityID;
        public float Interior01;
        public float HullLowPassHertz;
        public uint Frame;
        public byte Active;
        private byte _reserved0;
        private ushort _reserved1;
        private uint _reserved2;
        private uint _reserved3;
    }

    /// <summary>
    /// Cheap deterministic SDF stand-in. Negative sample means "solid between listener and source".
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public partial struct MockSDFSampler
    {
        public float3 Center;
        public float3 HalfExtents;
        public float WallPlaneY;
        public float WallThickness;
        public byte Enabled;
        public byte UseBox;
        private ushort _reserved0;
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

    [StructLayout(LayoutKind.Sequential, Size = 96)]
    public partial struct MockTerrainSampler
    {
        public MockSDFSampler SdfSampler;
        public float ReverbVolumeCubicMeters;
        public byte AcousticEnvironment;
        private byte _reserved0;
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
        public const int MaxPhysicalVoiceCount = 32;
        public const int LowTierPhysicalVoiceCount = 16;
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
        public static bool ResolveDearLieOcclusion(float3 listenerToSource, in MockSDFSampler sampler, int disableSdfOcclusion)
        {
            if (disableSdfOcclusion != 0 || sampler.Enabled == 0)
                return false;

            float signedDistance = sampler.Sample(listenerToSource * 0.5f);
            return math.isfinite(signedDistance) && signedDistance < 0f;
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
                if (keyHash == HashLowerAscii("speed_of_sound".AsSpan()) ||
                    keyHash == HashLowerAscii("sound_speed".AsSpan()))
                {
                    tuning.SoundSpeedMetersPerSecond = value;
                    any = true;
                }
                else if (keyHash == HashLowerAscii("global_occlusion_penalty".AsSpan()) ||
                    keyHash == HashLowerAscii("occlusion_gain".AsSpan()))
                {
                    tuning.GlobalOcclusionPenalty = value;
                    any = true;
                }
                else if (keyHash == HashLowerAscii("occluded_lowpass_hz".AsSpan()) ||
                    keyHash == HashLowerAscii("lowpass_hz".AsSpan()))
                {
                    tuning.OccludedLowPassHertz = value;
                    any = true;
                }
                else if (keyHash == HashLowerAscii("sabine_decay_times".AsSpan()) ||
                    keyHash == HashLowerAscii("sabine_decay_scale".AsSpan()))
                {
                    tuning.SabineDecayScale = value;
                    any = true;
                }
                else if (keyHash == HashLowerAscii("max_hydrated_voices".AsSpan()) ||
                    keyHash == HashLowerAscii("physical_voice_limit".AsSpan()))
                {
                    tuning.MaxHydratedVoices = (int)math.round(value);
                    any = true;
                }
                else if (keyHash == HashLowerAscii("disable_sdf_occlusion".AsSpan()))
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
    }
}
