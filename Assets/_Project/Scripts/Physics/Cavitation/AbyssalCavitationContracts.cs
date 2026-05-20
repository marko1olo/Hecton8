using System;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Reflection;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    public static class AbyssalCavitationConstants
    {
        public const int MaxShockwaves = 128;
        public const int MaxEntitySnapshots = 512;
        public const int MaxForcePackets = MaxEntitySnapshots;
        public const int MaxVisualSpheres = MaxShockwaves;
        public const int TelemetryCapacity = 300;
        public const int CounterBlockCount = 8;
        public const int OrdnanceProfileCapacity = 32;
        public const int CsvScratchBytes = 16384;
        public const int SdfDescriptorCount = 1;
        public const int SdfVolumeDimX = 32;
        public const int SdfVolumeDimY = 16;
        public const int SdfVolumeDimZ = 64;
        public const int SdfVoxelCapacity = SdfVolumeDimX * SdfVolumeDimY * SdfVolumeDimZ;
        public const float SafeLocalAupSpanMeters = 32768f;
        public const uint SourceHash = 0x53483135u; // SH15
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_156.bin";
    }

    internal static class AbyssalCavitationVaultBufferIds
    {
        public const BufferID ShockwaveEvents = (BufferID)71560;
        public const BufferID ShockwaveCounters = (BufferID)71561;
        public const BufferID EntitySnapshots = (BufferID)71562;
        public const BufferID ForcePackets = (BufferID)71563;
        public const BufferID VisualSpheres = (BufferID)71564;
        public const BufferID TelemetryRing = (BufferID)71565;
        public const BufferID OrdnanceProfiles = (BufferID)71566;
        public const BufferID CsvScratch = (BufferID)71567;
        public const BufferID Tuning = (BufferID)71568;
        public const BufferID SdfDescriptor = (BufferID)71569;
        public const BufferID SdfVoxels = (BufferID)71570;
    }

    internal static class AbyssalCavitationCounterIndex
    {
        public const int ActiveShockwaves = 0;
        public const int CandidateCount = 1;
        public const int ForcePacketCount = 2;
        public const int VisualCount = 3;
        public const int TelemetryHead = 4;
        public const int FaultFlags = 5;
        public const int CsvProfileCount = 6;
        public const int LastFrame = 7;
    }

    public static class AbyssalCavitationEntityFlags
    {
        public const uint Active = 1u << 0;
        public const uint Critical = 1u << 1;
        public const uint ForceReceiver = 1u << 2;
        public const uint NonFinite = 1u << 31;
    }

    public static class AbyssalCavitationPacketFlags
    {
        public const uint Active = 1u << 0;
        public const uint SdfDampened = 1u << 1;
        public const uint CriticalTarget = 1u << 2;
        public const uint ForceSaturated = 1u << 3;
        public const uint NonFiniteRecovered = 1u << 31;
    }

    public static class AbyssalCavitationTelemetryFlags
    {
        public const uint None = 0u;
        public const uint NonFiniteRecovered = 1u << 0;
        public const uint SdfDampened = 1u << 1;
        public const uint ForceSaturated = 1u << 2;
        public const uint MockFallback = 1u << 3;
    }

    public static class AbyssalCavitationSdfFlags
    {
        public const uint Active = 1u << 0;
        public const uint SignedDistanceBytes = 1u << 1;
        public const uint MockFallback = 1u << 2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ShockwaveEventDTO
    {
        [FieldOffset(0)] public double3 EpicenterAUP;
        [FieldOffset(24)] public float CurrentRadius;
        [FieldOffset(28)] public float MaxRadius;
        [FieldOffset(32)] public float PeakPressure;
        [FieldOffset(36)] public float ExpansionSpeed;
        [FieldOffset(40)] public uint SourceHashID;
        [FieldOffset(44)] public uint _pad0;
        [FieldOffset(48)] public ulong _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ShockwaveEntitySnapshotDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public float EffectiveArea;
        [FieldOffset(40)] public float InverseMass;
        [FieldOffset(44)] public int RigidbodySlot;
        [FieldOffset(48)] public uint EntityHash;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ShockwaveForcePacketDTO
    {
        [FieldOffset(0)] public double3 ApplicationAUP;
        [FieldOffset(24)] public float3 Force;
        [FieldOffset(36)] public float Pressure;
        [FieldOffset(40)] public int RigidbodySlot;
        [FieldOffset(44)] public uint TargetEntityHash;
        [FieldOffset(48)] public uint SourceHashID;
        [FieldOffset(52)] public uint FrameIndex;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public float SdfDampening;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CavitationVisualSphereDTO
    {
        [FieldOffset(0)] public float4 CenterRadius;
        [FieldOffset(16)] public float4 IntensityAgeQualityFlags;
        [FieldOffset(32)] public float4 CurlPhase;
        [FieldOffset(48)] public float4 Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ShockwaveTelemetryEntry
    {
        [FieldOffset(0)] public double3 EpicenterAUP;
        [FieldOffset(24)] public float CurrentRadius;
        [FieldOffset(28)] public float PeakPressure;
        [FieldOffset(32)] public float PeakForce;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public uint FrameIndex;
        [FieldOffset(44)] public uint StateHash;
        [FieldOffset(48)] public int ActiveShockwaves;
        [FieldOffset(52)] public int CandidateCount;
        [FieldOffset(56)] public float CpuMicroseconds;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ShockwaveCounterBlock
    {
        [FieldOffset(0)] public int Value;
        [FieldOffset(4)] public uint _pad0;
        [FieldOffset(8)] public ulong _pad1;
        [FieldOffset(16)] public ulong _pad2;
        [FieldOffset(24)] public ulong _pad3;
        [FieldOffset(32)] public ulong _pad4;
        [FieldOffset(40)] public ulong _pad5;
        [FieldOffset(48)] public ulong _pad6;
        [FieldOffset(56)] public ulong _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AbyssalCavitationTuningDTO
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public float ForceScale;
        [FieldOffset(8)] public float MinPressure;
        [FieldOffset(12)] public float SdfHardDampening;
        [FieldOffset(16)] public float SdfSoftnessMeters;
        [FieldOffset(20)] public float VisualIntensityScale;
        [FieldOffset(24)] public float MockSeafloorY;
        [FieldOffset(28)] public float MaxForceNewton;
        [FieldOffset(32)] public float SimulationTickDelta;
        [FieldOffset(36)] public float CavitationShellMeters;
        [FieldOffset(40)] public uint SectorHash;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct OrdnanceProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public uint SourceHash;
        [FieldOffset(8)] public float PeakPressure;
        [FieldOffset(12)] public float MaxRadius;
        [FieldOffset(16)] public float ExpansionSpeed;
        [FieldOffset(20)] public float VisualIntensity;
        [FieldOffset(24)] public float ForceScale;
        [FieldOffset(28)] public float AcousticIntensity;
        [FieldOffset(32)] public ulong _pad0;
        [FieldOffset(40)] public ulong _pad1;
        [FieldOffset(48)] public ulong _pad2;
        [FieldOffset(56)] public ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AbyssalCavitationSdfVolumeDTO
    {
        [FieldOffset(0)] public double3 OriginAUP;
        [FieldOffset(24)] public float3 CellSizeMeters;
        [FieldOffset(36)] public int3 Dimensions;
        [FieldOffset(48)] public float DecodeRangeMeters;
        [FieldOffset(52)] public uint Version;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint _pad0;
    }

    public static class AbyssalCavitationLayout
    {
        public const int ShockwaveEventSize = 64;
        public const int EntitySnapshotSize = 64;
        public const int ForcePacketSize = 64;
        public const int VisualSphereSize = 64;
        public const int TelemetryEntrySize = 64;
        public const int CounterBlockSize = 64;
        public const int TuningSize = 64;
        public const int OrdnanceProfileSize = 64;
        public const int SdfVolumeSize = 64;

        public static bool Validate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return UnsafeUtility.SizeOf<ShockwaveEventDTO>() == ShockwaveEventSize &&
                   UnsafeUtility.GetFieldOffset(Field<ShockwaveEventDTO>(nameof(ShockwaveEventDTO.EpicenterAUP))) == 0 &&
                   UnsafeUtility.GetFieldOffset(Field<ShockwaveEventDTO>(nameof(ShockwaveEventDTO.CurrentRadius))) == 24 &&
                   UnsafeUtility.GetFieldOffset(Field<ShockwaveEventDTO>(nameof(ShockwaveEventDTO.MaxRadius))) == 28 &&
                   UnsafeUtility.GetFieldOffset(Field<ShockwaveEventDTO>(nameof(ShockwaveEventDTO.PeakPressure))) == 32 &&
                   UnsafeUtility.GetFieldOffset(Field<ShockwaveEventDTO>(nameof(ShockwaveEventDTO.ExpansionSpeed))) == 36 &&
                   UnsafeUtility.GetFieldOffset(Field<ShockwaveEventDTO>(nameof(ShockwaveEventDTO.SourceHashID))) == 40 &&
                   UnsafeUtility.GetFieldOffset(Field<ShockwaveEventDTO>(nameof(ShockwaveEventDTO._pad0))) == 44 &&
                   UnsafeUtility.GetFieldOffset(Field<ShockwaveEventDTO>(nameof(ShockwaveEventDTO._pad1))) == 48 &&
                   UnsafeUtility.GetFieldOffset(Field<ShockwaveEventDTO>(nameof(ShockwaveEventDTO._pad2))) == 56 &&
                   UnsafeUtility.SizeOf<ShockwaveEntitySnapshotDTO>() == EntitySnapshotSize &&
                   UnsafeUtility.SizeOf<ShockwaveForcePacketDTO>() == ForcePacketSize &&
                   UnsafeUtility.SizeOf<CavitationVisualSphereDTO>() == VisualSphereSize &&
                   UnsafeUtility.SizeOf<ShockwaveTelemetryEntry>() == TelemetryEntrySize &&
                   UnsafeUtility.SizeOf<ShockwaveCounterBlock>() == CounterBlockSize &&
                   UnsafeUtility.SizeOf<AbyssalCavitationTuningDTO>() == TuningSize &&
                   UnsafeUtility.SizeOf<OrdnanceProfileDTO>() == OrdnanceProfileSize &&
                   UnsafeUtility.SizeOf<AbyssalCavitationSdfVolumeDTO>() == SdfVolumeSize &&
                   UnsafeUtility.GetFieldOffset(Field<AbyssalCavitationSdfVolumeDTO>(nameof(AbyssalCavitationSdfVolumeDTO.OriginAUP))) == 0 &&
                   UnsafeUtility.GetFieldOffset(Field<AbyssalCavitationSdfVolumeDTO>(nameof(AbyssalCavitationSdfVolumeDTO.CellSizeMeters))) == 24 &&
                   UnsafeUtility.GetFieldOffset(Field<AbyssalCavitationSdfVolumeDTO>(nameof(AbyssalCavitationSdfVolumeDTO.Dimensions))) == 36 &&
                   UnsafeUtility.GetFieldOffset(Field<AbyssalCavitationSdfVolumeDTO>(nameof(AbyssalCavitationSdfVolumeDTO.DecodeRangeMeters))) == 48 &&
                   UnsafeUtility.GetFieldOffset(Field<AbyssalCavitationSdfVolumeDTO>(nameof(AbyssalCavitationSdfVolumeDTO.Version))) == 52 &&
                   UnsafeUtility.GetFieldOffset(Field<AbyssalCavitationSdfVolumeDTO>(nameof(AbyssalCavitationSdfVolumeDTO.Flags))) == 56 &&
                   UnsafeUtility.GetFieldOffset(Field<AbyssalCavitationSdfVolumeDTO>(nameof(AbyssalCavitationSdfVolumeDTO._pad0))) == 60;
#else
            return true;
#endif
        }

        public static void ValidateOrThrow()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Validate())
                throw new InvalidOperationException("SHINOBU_156 cavitation DTO layout mismatch. Expected explicit 64-byte cache-line DTOs.");
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static FieldInfo Field<T>(string name)
        {
            return typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
#endif
    }

    public static class AbyssalCavitationSanitizer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AbyssalCavitationTuningDTO DefaultTuning(float globalQualityWeight)
        {
            AbyssalCavitationTuningDTO tuning = default;
            tuning.GlobalQualityWeight = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            tuning.ForceScale = 0.018f;
            tuning.MinPressure = 0.035f;
            tuning.SdfHardDampening = 0.12f;
            tuning.SdfSoftnessMeters = 4.0f;
            tuning.VisualIntensityScale = 1.0f;
            tuning.MockSeafloorY = -34.0f;
            tuning.MaxForceNewton = 45000.0f;
            tuning.SimulationTickDelta = 0.02f;
            tuning.CavitationShellMeters = 1.25f;
            tuning.SectorHash = 0x5348494Eu;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AbyssalCavitationTuningDTO SanitizeTuning(AbyssalCavitationTuningDTO value)
        {
            AbyssalCavitationTuningDTO fallback = DefaultTuning(1f);
            value.GlobalQualityWeight = Sanitize01(value.GlobalQualityWeight, fallback.GlobalQualityWeight);
            value.ForceScale = SanitizePositive(value.ForceScale, fallback.ForceScale, 0.00001f, 1.0f);
            value.MinPressure = SanitizePositive(value.MinPressure, fallback.MinPressure, 0.0f, 1000000.0f);
            value.SdfHardDampening = Sanitize01(value.SdfHardDampening, fallback.SdfHardDampening);
            value.SdfSoftnessMeters = SanitizePositive(value.SdfSoftnessMeters, fallback.SdfSoftnessMeters, 0.05f, 64f);
            value.VisualIntensityScale = SanitizePositive(value.VisualIntensityScale, fallback.VisualIntensityScale, 0.0f, 8f);
            value.MockSeafloorY = math.isfinite(value.MockSeafloorY) ? value.MockSeafloorY : fallback.MockSeafloorY;
            value.MaxForceNewton = SanitizePositive(value.MaxForceNewton, fallback.MaxForceNewton, 1f, 10000000f);
            value.SimulationTickDelta = SanitizePositive(value.SimulationTickDelta, fallback.SimulationTickDelta, 0.0001f, 0.1f);
            value.CavitationShellMeters = SanitizePositive(value.CavitationShellMeters, fallback.CavitationShellMeters, 0.05f, 32f);
            value.SectorHash = value.SectorHash != 0u ? value.SectorHash : fallback.SectorHash;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OrdnanceProfileDTO SanitizeProfile(OrdnanceProfileDTO value)
        {
            value.PeakPressure = SanitizePositive(value.PeakPressure, 9000f, 1f, 100000000f);
            value.MaxRadius = SanitizePositive(value.MaxRadius, 24f, 1f, 2048f);
            value.ExpansionSpeed = SanitizePositive(value.ExpansionSpeed, 220f, 1f, 2000f);
            value.VisualIntensity = SanitizePositive(value.VisualIntensity, 1f, 0f, 16f);
            value.ForceScale = SanitizePositive(value.ForceScale, 0.018f, 0.00001f, 1f);
            value.AcousticIntensity = Sanitize01(value.AcousticIntensity, 0.7f);
            value.SourceHash = value.SourceHash != 0u ? value.SourceHash : AbyssalCavitationConstants.SourceHash;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value, float fallback)
        {
            return math.saturate(math.isfinite(value) ? value : fallback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float value, float fallback, float minimum, float maximum)
        {
            float safe = math.isfinite(value) ? value : fallback;
            return math.clamp(safe, minimum, maximum);
        }
    }

    public static class AbyssalCavitationOrdnanceCsv
    {
        public static int Parse(ReadOnlySpan<byte> csvBytes, NativeArray<OrdnanceProfileDTO> profiles)
        {
            if (!profiles.IsCreated || profiles.Length == 0)
                return 0;

            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;

            if (csvBytes.Length == 0)
                return 0;

            int count = 0;
            int cursor = 0;
            while (count < profiles.Length && TryReadLine(csvBytes, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = Trim(line);
                if (line.Length == 0 || line[0] == (byte)'#' || StartsWithHeader(line))
                    continue;

                if (!TryParseLine(line, out OrdnanceProfileDTO profile))
                    continue;

                if (TryInsertProfile(profiles, AbyssalCavitationSanitizer.SanitizeProfile(profile)))
                    count++;
            }

            return count;
        }

        public static uint HashLowerAscii(ReadOnlySpan<byte> text)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                byte c = text[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash != 0u ? hash : 2166136261u;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out OrdnanceProfileDTO profile)
        {
            profile = default;
            int cursor = 0;
            ReadOnlySpan<byte> name = ReadCell(line, ref cursor);
            if (name.Length == 0)
                return false;

            profile.ProfileHash = HashLowerAscii(name);
            profile.SourceHash = profile.ProfileHash ^ AbyssalCavitationConstants.SourceHash;
            profile.PeakPressure = TryReadFloat(line, ref cursor, 9000f);
            profile.MaxRadius = TryReadFloat(line, ref cursor, 24f);
            profile.ExpansionSpeed = TryReadFloat(line, ref cursor, 220f);
            profile.VisualIntensity = TryReadFloat(line, ref cursor, 1f);
            profile.ForceScale = TryReadFloat(line, ref cursor, 0.018f);
            profile.AcousticIntensity = TryReadFloat(line, ref cursor, 0.7f);
            return true;
        }

        public static bool TryFindProfile(NativeArray<OrdnanceProfileDTO> profiles, uint profileHash, out OrdnanceProfileDTO profile)
        {
            profile = default;
            if (!profiles.IsCreated || profiles.Length == 0 || profileHash == 0u)
                return false;

            int start = (int)(profileHash % (uint)profiles.Length);
            for (int probe = 0; probe < profiles.Length; probe++)
            {
                int slot = start + probe;
                if (slot >= profiles.Length)
                    slot -= profiles.Length;

                OrdnanceProfileDTO candidate = profiles[slot];
                if (candidate.ProfileHash == profileHash)
                {
                    profile = candidate;
                    return true;
                }

                if (candidate.ProfileHash == 0u)
                    return false;
            }

            return false;
        }

        private static bool TryInsertProfile(NativeArray<OrdnanceProfileDTO> profiles, OrdnanceProfileDTO profile)
        {
            if (profile.ProfileHash == 0u)
                return false;

            int start = (int)(profile.ProfileHash % (uint)profiles.Length);
            for (int probe = 0; probe < profiles.Length; probe++)
            {
                int slot = start + probe;
                if (slot >= profiles.Length)
                    slot -= profiles.Length;

                OrdnanceProfileDTO existing = profiles[slot];
                if (existing.ProfileHash == 0u || existing.ProfileHash == profile.ProfileHash)
                {
                    profiles[slot] = profile;
                    return existing.ProfileHash == 0u;
                }
            }

            return false;
        }

        private static bool StartsWithHeader(ReadOnlySpan<byte> line)
        {
            int headerCursor = 0;
            ReadOnlySpan<byte> first = ReadCell(line, ref headerCursor);
            return first.Length >= 4 &&
                   Lower(first[0]) == (byte)'n' &&
                   Lower(first[1]) == (byte)'a' &&
                   Lower(first[2]) == (byte)'m' &&
                   Lower(first[3]) == (byte)'e';
        }

        private static float TryReadFloat(ReadOnlySpan<byte> line, ref int cursor, float fallback)
        {
            ReadOnlySpan<byte> token = ReadCell(line, ref cursor);
            return TryParseFloat(token, out float value) ? value : fallback;
        }

        private static ReadOnlySpan<byte> ReadCell(ReadOnlySpan<byte> line, ref int cursor)
        {
            if (cursor >= line.Length)
                return ReadOnlySpan<byte>.Empty;

            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;

            return Trim(line.Slice(start, end - start));
        }

        private static bool TryReadLine(ReadOnlySpan<byte> text, ref int cursor, out ReadOnlySpan<byte> line)
        {
            line = ReadOnlySpan<byte>.Empty;
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

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> text)
        {
            int start = 0;
            int end = text.Length - 1;
            while (start <= end && text[start] <= 32)
                start++;
            while (end >= start && text[end] <= 32)
                end--;
            return start <= end ? text.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            token = Trim(token);
            if (token.Length == 0)
                return false;

            int sign = 1;
            int index = 0;
            if (token[0] == (byte)'-')
            {
                sign = -1;
                index = 1;
            }
            else if (token[0] == (byte)'+')
            {
                index = 1;
            }

            double result = 0.0;
            bool any = false;
            while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
            {
                any = true;
                result = result * 10.0 + token[index] - (byte)'0';
                index++;
            }

            if (index < token.Length && token[index] == (byte)'.')
            {
                index++;
                double scale = 0.1;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    any = true;
                    result += (token[index] - (byte)'0') * scale;
                    scale *= 0.1;
                    index++;
                }
            }

            if (!any)
                return false;

            if (index < token.Length && (token[index] == (byte)'e' || token[index] == (byte)'E'))
            {
                index++;
                int exponentSign = 1;
                if (index < token.Length && token[index] == (byte)'-')
                {
                    exponentSign = -1;
                    index++;
                }
                else if (index < token.Length && token[index] == (byte)'+')
                {
                    index++;
                }

                int exponent = 0;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    exponent = exponent * 10 + token[index] - (byte)'0';
                    index++;
                }

                result *= math.pow(10.0, exponent * exponentSign);
            }

            value = (float)(result * sign);
            return math.isfinite(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte Lower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }
    }
}
