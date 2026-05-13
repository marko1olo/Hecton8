using System.Runtime.InteropServices;
using Hecton8.Audio.Propagation;
using Unity.Mathematics;

namespace Hecton8.Audio.Virtualization
{
    /// <summary>
    /// Registry-published virtual voice scheduler. Implementations must keep enqueue,
    /// sort, and selection paths allocation-free after explicit initialization.
    /// </summary>
    public interface IAudioVirtualizationService
    {
        /// <summary>True when native buffers and channel state are ready.</summary>
        bool IsVirtualizationReady { get; }

        /// <summary>Current physical voice cap. Low tier is 8, other tiers are 16.</summary>
        int PhysicalVoiceLimit { get; }

        /// <summary>Virtual voices accepted into the current write buffer.</summary>
        int VirtualVoiceCount { get; }

        /// <summary>Physical voices selected by the last sort pass.</summary>
        int ActivePhysicalVoiceCount { get; }

        /// <summary>Voices rejected as inaudible by the last sort pass.</summary>
        int CulledVoiceCount { get; }

        /// <summary>Audible voices not mapped to physical channels by the last sort pass.</summary>
        int StolenVoiceCount { get; }

        /// <summary>Voices rejected because the virtual queue was full.</summary>
        int DroppedVoiceCount { get; }

        /// <summary>Enqueues a virtual sound emission before physical DSP assignment.</summary>
        bool EnqueueVirtualVoice(in VirtualVoiceRequest request);

        /// <summary>Updates the listener AUP used by the next sort pass.</summary>
        void SetVirtualListener(in AcousticAup listenerAup);

        /// <summary>Applies the current hardware tier voice cap.</summary>
        void SetLowTierVirtualization(bool lowTier);

        /// <summary>Applies a synchronous origin-shift delta to buffered virtual voices.</summary>
        void ApplyVirtualVoiceAupShift(long gridDeltaX, long gridDeltaY, long gridDeltaZ);

        /// <summary>Copies the last sort statistics without allocation.</summary>
        bool TryGetVirtualizationStats(out VirtualVoiceStatistics statistics);
    }

    /// <summary>
    /// Blittable request queued by gameplay audio emitters before physical channel selection.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VirtualVoiceRequest
    {
        public uint EventID;
        public uint ClipHash;
        public AcousticAup SourceAup;
        public float Volume;
        public float Priority;
        public float Pitch;
        public float DopplerRatio;
        public int StationaryCacheKey;
        public AcousticPortalFlags PortalFlags;
        public byte FoveatedTier;
        private byte _reserved0;
        private ushort _reserved1;

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
        {
            EventID = eventID;
            ClipHash = clipHash;
            SourceAup = sourceAup;
            Volume = volume;
            Priority = priority;
            Pitch = pitch;
            DopplerRatio = dopplerRatio;
            StationaryCacheKey = stationaryCacheKey;
            PortalFlags = portalFlags;
            FoveatedTier = foveatedTier;
            _reserved0 = 0;
            _reserved1 = 0;
        }
    }

    /// <summary>
    /// Mutable virtual voice state consumed by the Burst ranking job.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VirtualVoice
    {
        public uint EventID;
        public uint ClipHash;
        public uint StableKey;
        public AcousticAup SourceAup;
        public float Volume;
        public float Priority;
        public float Pitch;
        public float DopplerRatio;
        public float Attenuation;
        public float Weight;
        public float DistanceSq;
        public int StationaryCacheKey;
        public AcousticPortalFlags PortalFlags;
        public byte FoveatedTier;
        private byte _reserved0;
        private ushort _reserved1;
    }

    /// <summary>
    /// Selected physical channel candidate after virtual voice ranking.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VirtualVoiceSelection
    {
        public uint EventID;
        public uint ClipHash;
        public uint StableKey;
        public AcousticAup SourceAup;
        public float Volume;
        public float Pitch;
        public float DopplerRatio;
        public float Attenuation;
        public float Weight;
        public float DistanceSq;
        public int StationaryCacheKey;
        public AcousticPortalFlags PortalFlags;
        public byte FoveatedTier;
        private byte _reserved0;
        private ushort _reserved1;
    }

    /// <summary>
    /// Last virtual voice sort pass counters.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
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
    }

    /// <summary>
    /// Fixed-size black-box entry for the last 300 virtual voice frames.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct VirtualVoiceTelemetryEntry
    {
        public int Frame;
        public ushort TotalVoices;
        public ushort AudibleVoices;
        public ushort CulledVoices;
        public ushort ActiveVoices;
        public ushort PhysicalVoiceLimit;
        public ushort StolenVoices;
        public ushort DroppedVoices;
        public ushort Flags;
        public uint StateHash;
        public float LoudestWeight;
    }

    /// <summary>
    /// Shared virtual voice math helpers.
    /// </summary>
    public static class VirtualVoiceUtility
    {
        public const int FoveatedTierFrozen = 2;
        public const float MinimumAudibleEnergy = 0.01f;
        public const float MinimumDopplerRatio = 0.1f;
        public const float MaximumDopplerRatio = 3f;

        public static uint ComputeStableKey(uint eventID, uint clipHash, in AcousticAup sourceAup)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ eventID) * 16777619u;
                hash = (hash ^ clipHash) * 16777619u;
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
    }
}
