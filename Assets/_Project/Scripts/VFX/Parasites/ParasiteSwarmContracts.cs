using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.VFX.Parasites
{
    public static unsafe class ParasiteSwarmContracts
    {
        public const int MaxGpuParticleCapacity = 2000000;
        public const int MinGpuParticleCapacity = 5000;
        public const int MaxTargetCount = 16;
        public const int CandidateCapacity = 512;
        public const int MaxThermalCellScanCount = 256;
        public const int TelemetryCapacity = 300;
        public const int ProfileCapacity = 32;
        public const int CsvScratchBytes = 16 * 1024;
        public const int ScannerSummaryCapacity = 8;
        public const int TelemetryDumpHeaderBytes = 64;
        public const uint TelemetryDumpMagic = 0x33503848u; // H8P3 little-endian bytes.
        public const uint TelemetryDumpVersion = 1u;

        public const uint TelemetryFlagTimingEstimated = 1u << 0;
        public const uint TelemetryFlagTargetOverflow = 1u << 1;
        public const uint TelemetryFlagGpuBudgetSpike = 1u << 2;
        public const uint TelemetryFlagInvalidMath = 1u << 3;
        public const uint TelemetryFlagMockTargets = 1u << 4;
        public const uint TelemetryFlagNoCompute = 1u << 5;
        public const uint TelemetryFlagDumped = 1u << 6;

        public const uint TuningFlagUseQualityOverride = 1u << 0;
        public const uint TuningFlagMockTargets = 1u << 1;

        public const uint DefaultProfileHash = 0x50C8A313u;
        private const uint HashSeed = 2166136261u;
        private const uint HashPrime = 16777619u;
        private const float Pi = 3.14159265f;
        private const float HalfPi = 1.57079633f;
        private const float Tau = 6.28318531f;
        private const float InvTau = 0.15915494f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ValidateRuntimeLayouts(out int failureCode)
        {
            failureCode = 0;
            if (UnsafeUtility.SizeOf<ParasiteTargetDTO>() != 32 ||
                Marshal.OffsetOf<ParasiteTargetDTO>(nameof(ParasiteTargetDTO.LocalPosition)).ToInt32() != 0 ||
                Marshal.OffsetOf<ParasiteTargetDTO>(nameof(ParasiteTargetDTO.ThermalSignature)).ToInt32() != 12 ||
                Marshal.OffsetOf<ParasiteTargetDTO>(nameof(ParasiteTargetDTO.Velocity)).ToInt32() != 16 ||
                Marshal.OffsetOf<ParasiteTargetDTO>(nameof(ParasiteTargetDTO.AttractionRadius)).ToInt32() != 28)
            {
                failureCode = 1;
                return false;
            }

            if (UnsafeUtility.SizeOf<ParasiteTargetCandidateDTO>() != 64)
            {
                failureCode = 2;
                return false;
            }

            if (UnsafeUtility.SizeOf<SwarmTelemetryEntry>() != 64)
            {
                failureCode = 3;
                return false;
            }

            if (UnsafeUtility.SizeOf<ParasiteSwarmTuningDTO>() != 64)
            {
                failureCode = 4;
                return false;
            }

            if (UnsafeUtility.SizeOf<ParasiteBehaviorProfileDTO>() != 64)
            {
                failureCode = 5;
                return false;
            }

            if (UnsafeUtility.SizeOf<ParasiteFrameParamsDTO>() != 64)
            {
                failureCode = 6;
                return false;
            }

            return true;
        }

        public static void EnsureVaultBuffers(IDataVault vault)
        {
            if (vault == null)
                return;

            vault.EnsureGenerationHandle<ParasiteTargetDTO>(BufferID.ShinobuParasiteTargets, MaxTargetCount, SystemID.Vfx, NativeArrayOptions.ClearMemory);
            vault.EnsureGenerationHandle<ParasiteTargetCandidateDTO>(BufferID.ShinobuParasiteTargetCandidates, CandidateCapacity, SystemID.Vfx, NativeArrayOptions.ClearMemory);
            vault.EnsureGenerationHandle<int>(BufferID.ShinobuParasiteTargetCount, 1, SystemID.Vfx, NativeArrayOptions.ClearMemory);
            vault.EnsureGenerationHandle<ParasiteSwarmTuningDTO>(BufferID.ShinobuParasiteTuning, 1, SystemID.Vfx, NativeArrayOptions.ClearMemory);
            vault.EnsureGenerationHandle<SwarmTelemetryEntry>(BufferID.ShinobuParasiteTelemetryRing, TelemetryCapacity, SystemID.Vfx, NativeArrayOptions.ClearMemory);
            vault.EnsureGenerationHandle<int>(BufferID.ShinobuParasiteTelemetryCursor, 1, SystemID.Vfx, NativeArrayOptions.ClearMemory);
            vault.EnsureGenerationHandle<ParasiteBehaviorProfileDTO>(BufferID.ShinobuParasiteProfiles, ProfileCapacity, SystemID.Vfx, NativeArrayOptions.ClearMemory);
            vault.EnsureGenerationHandle<byte>(BufferID.ShinobuParasiteCsvScratch, CsvScratchBytes, SystemID.Vfx, NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<ParasiteScannerSummaryDTO>(BufferID.ShinobuParasiteScannerSummary, ScannerSummaryCapacity, SystemID.Vfx, NativeArrayOptions.ClearMemory);
            vault.EnsureGenerationHandle<int>(BufferID.ShinobuParasiteProfileCount, 1, SystemID.Vfx, NativeArrayOptions.ClearMemory);
        }

        public static ParasiteSwarmTuningDTO DefaultTuning()
        {
            return new ParasiteSwarmTuningDTO
            {
                ThermalAttractionMultiplier = 18f,
                CurlNoiseFrequency = 0.18f,
                SwarmMaxSpeed = 14f,
                GlobalQualityOverride = 1f,
                ParasiteAttractionThreshold = 8f,
                MinParticleBudget = MinGpuParticleCapacity,
                MaxParticleBudget = MaxGpuParticleCapacity,
                AttractionRadiusScale = 1f,
                CurlStrength = 3.5f,
                FlowFieldWeight = 0.6f,
                AttachmentShellRadius = 2.4f,
                TargetVelocityBlend = 0.92f,
                Version = 1u,
                Flags = 0u,
                ActiveProfileHash = DefaultProfileHash
            };
        }

        public static ParasiteSwarmTuningDTO Sanitize(ParasiteSwarmTuningDTO value)
        {
            ParasiteSwarmTuningDTO fallback = DefaultTuning();
            value.ThermalAttractionMultiplier = FinitePositive(value.ThermalAttractionMultiplier, fallback.ThermalAttractionMultiplier, 0.01f, 256f);
            value.CurlNoiseFrequency = FinitePositive(value.CurlNoiseFrequency, fallback.CurlNoiseFrequency, 0.001f, 6f);
            value.SwarmMaxSpeed = FinitePositive(value.SwarmMaxSpeed, fallback.SwarmMaxSpeed, 0.01f, 128f);
            value.GlobalQualityOverride = math.saturate(FiniteOr(value.GlobalQualityOverride, fallback.GlobalQualityOverride));
            value.ParasiteAttractionThreshold = FinitePositive(value.ParasiteAttractionThreshold, fallback.ParasiteAttractionThreshold, -120f, 400f);
            value.MinParticleBudget = math.clamp(value.MinParticleBudget <= 0 ? fallback.MinParticleBudget : value.MinParticleBudget, 1, MaxGpuParticleCapacity);
            value.MaxParticleBudget = math.clamp(value.MaxParticleBudget <= 0 ? fallback.MaxParticleBudget : value.MaxParticleBudget, value.MinParticleBudget, MaxGpuParticleCapacity);
            value.AttractionRadiusScale = FinitePositive(value.AttractionRadiusScale, fallback.AttractionRadiusScale, 0.01f, 64f);
            value.CurlStrength = FinitePositive(value.CurlStrength, fallback.CurlStrength, 0f, 64f);
            value.FlowFieldWeight = FinitePositive(value.FlowFieldWeight, fallback.FlowFieldWeight, 0f, 8f);
            value.AttachmentShellRadius = FinitePositive(value.AttachmentShellRadius, fallback.AttachmentShellRadius, 0.1f, 32f);
            value.TargetVelocityBlend = math.saturate(FiniteOr(value.TargetVelocityBlend, fallback.TargetVelocityBlend));
            value.Version = value.Version == 0u ? fallback.Version : value.Version;
            value.ActiveProfileHash = value.ActiveProfileHash == 0u ? fallback.ActiveProfileHash : value.ActiveProfileHash;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveQuality01(in ParasiteSwarmTuningDTO tuning, float globalQualityWeight)
        {
            float baseQuality = math.saturate(FiniteOr(globalQualityWeight, 1f));
            float overrideQuality = math.saturate(FiniteOr(tuning.GlobalQualityOverride, baseQuality));
            return (tuning.Flags & TuningFlagUseQualityOverride) != 0u ? overrideQuality : baseQuality;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothQuality01(float quality01)
        {
            float q = math.saturate(FiniteOr(quality01, 0f));
            return q * q * (3f - (2f * q));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastSinApprox(float value)
        {
            if (!math.isfinite(value))
                return 0f;

            float x = value - (Tau * math.floor((value + Pi) * InvTau));
            float x2 = x * x;
            return x * (1f - (x2 * (0.16666667f - (x2 * (0.008333331f - (x2 * 0.0001984127f))))));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastCosApprox(float value)
        {
            return FastSinApprox(value + HalfPi);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveParticleBudget(float quality01, in ParasiteSwarmTuningDTO tuning)
        {
            float q = SmoothQuality01(quality01);
            int min = math.clamp(tuning.MinParticleBudget, 1, MaxGpuParticleCapacity);
            int max = math.clamp(tuning.MaxParticleBudget, min, MaxGpuParticleCapacity);
            return math.clamp((int)math.round(math.lerp(min, max, q)), 1, MaxGpuParticleCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EstimateGpuMicroseconds(int particleBudget, int targetCount, float quality01)
        {
            if (particleBudget <= 0 || targetCount <= 0)
                return 4;

            float q = SmoothQuality01(quality01);
            float perParticle = math.lerp(0.0065f, 0.0185f, q);
            float targetCost = math.max(1, targetCount) * math.lerp(0.18f, 0.46f, q);
            return (int)math.ceil((particleBudget * perParticle) + targetCost + 12f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashState(uint frame, int targetCount, int particleBudget, float quality01, uint tuningVersion)
        {
            uint h = HashSeed;
            h = Mix(h, frame);
            h = Mix(h, (uint)targetCount);
            h = Mix(h, (uint)particleBudget);
            h = Mix(h, math.asuint(quality01));
            h = Mix(h, tuningVersion);
            return h == 0u ? 1u : h;
        }

#if UNITY_EDITOR
        public static int LoadProfilesFromCsv(IDataVault vault, ReadOnlySpan<byte> bytes)
        {
            if (vault == null || bytes.Length <= 0)
                return 0;

            EnsureVaultBuffers(vault);
            if (!vault.TryGetGenerationHandle(BufferID.ShinobuParasiteProfiles, out VaultGenerationHandle<ParasiteBehaviorProfileDTO> profileHandle) ||
                !vault.TryGetGenerationHandle(BufferID.ShinobuParasiteProfileCount, out VaultGenerationHandle<int> countHandle))
            {
                return 0;
            }

            bool profileLocked = vault.TryAcquireWriteLock(in profileHandle, SystemID.Vfx, out NativeArray<ParasiteBehaviorProfileDTO> profiles);
            bool countLocked = vault.TryAcquireWriteLock(in countHandle, SystemID.Vfx, out NativeArray<int> profileCount);
            if (!profileLocked || !countLocked)
            {
                if (countLocked)
                    vault.ReleaseWriteLock(in countHandle, SystemID.Vfx);
                if (profileLocked)
                    vault.ReleaseWriteLock(in profileHandle, SystemID.Vfx);
                return 0;
            }

            int written = 0;
            try
            {
                int lineStart = 0;
                bool skippedHeader = false;
                for (int i = 0; i <= bytes.Length && written < math.min(ProfileCapacity, profiles.Length); i++)
                {
                    bool atEnd = i == bytes.Length;
                    if (!atEnd && bytes[i] != (byte)'\n')
                        continue;

                    int lineEnd = i;
                    if (lineEnd > lineStart && bytes[lineEnd - 1] == (byte)'\r')
                        lineEnd--;

                    ReadOnlySpan<byte> line = bytes.Slice(lineStart, math.max(0, lineEnd - lineStart));
                    lineStart = i + 1;
                    if (line.Length <= 0 || line[0] == (byte)'#')
                        continue;

                    if (!skippedHeader && IsProfileHeader(line))
                    {
                        skippedHeader = true;
                        continue;
                    }

                    if (TryParseProfileLine(line, out ParasiteBehaviorProfileDTO dto))
                    {
                        profiles[written] = dto;
                        written++;
                    }
                }

                if (profileCount.Length > 0)
                    profileCount[0] = written;
            }
            finally
            {
                vault.ReleaseWriteLock(in countHandle, SystemID.Vfx);
                vault.ReleaseWriteLock(in profileHandle, SystemID.Vfx);
            }

            return written;
        }

        public static bool TryWriteTelemetryDump(string projectRoot, NativeArray<SwarmTelemetryEntry> ring, int cursor)
        {
            if (!ring.IsCreated || ring.Length <= 0)
                return false;

            string root = string.IsNullOrEmpty(projectRoot) ? Directory.GetCurrentDirectory() : projectRoot;
            string directory = Path.Combine(root, "Docs", "AgentLogs");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "Dump_SHINOBU_313.bin");
            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ring);
            int bytes = ring.Length * UnsafeUtility.SizeOf<SwarmTelemetryEntry>();
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            Span<byte> header = stackalloc byte[TelemetryDumpHeaderBytes];
            WriteUInt32LE(header, 0, TelemetryDumpMagic);
            WriteUInt32LE(header, 4, TelemetryDumpVersion);
            WriteUInt32LE(header, 8, (uint)TelemetryDumpHeaderBytes);
            WriteUInt32LE(header, 12, (uint)UnsafeUtility.SizeOf<SwarmTelemetryEntry>());
            WriteUInt32LE(header, 16, (uint)ring.Length);
            WriteUInt32LE(header, 20, (uint)math.clamp(cursor, 0, ring.Length - 1));
            WriteUInt32LE(header, 24, (uint)bytes);
            stream.Write(header);
            stream.Write(new ReadOnlySpan<byte>(ptr, bytes));
            return true;
        }

        private static bool TryParseProfileLine(ReadOnlySpan<byte> line, out ParasiteBehaviorProfileDTO dto)
        {
            dto = default;
            Span<int2> ranges = stackalloc int2[6];
            int rangeCount = SplitCsv(line, ranges);
            if (rangeCount < 5)
                return false;

            ReadOnlySpan<byte> species = SliceRange(line, ranges[0]);
            if (species.Length <= 0)
                return false;

            dto.SpeciesHash = Fnv1a(species);
            dto.EmissionRate = ParseFloat(SliceRange(line, ranges[1]), 18f);
            dto.AttractionRadius = ParseFloat(SliceRange(line, ranges[2]), 3f);
            dto.NoiseMultiplier = ParseFloat(SliceRange(line, ranges[3]), 1f);
            dto.MaxSpeed = ParseFloat(SliceRange(line, ranges[4]), 14f);
            dto.ThermalBias = rangeCount > 5 ? ParseFloat(SliceRange(line, ranges[5]), 1f) : 1f;
            dto.Version = 1u;
            return dto.SpeciesHash != 0u;
        }

        private static int SplitCsv(ReadOnlySpan<byte> line, Span<int2> ranges)
        {
            int start = 0;
            int count = 0;
            for (int i = 0; i <= line.Length && count < ranges.Length; i++)
            {
                if (i != line.Length && line[i] != (byte)',')
                    continue;

                int end = i;
                while (start < end && line[start] <= 32)
                    start++;
                while (end > start && line[end - 1] <= 32)
                    end--;
                ranges[count++] = new int2(start, end);
                start = i + 1;
            }

            return count;
        }

        private static ReadOnlySpan<byte> SliceRange(ReadOnlySpan<byte> line, int2 range)
        {
            int start = range.x;
            int end = range.y;
            return line.Slice(start, math.max(0, end - start));
        }

        private static bool IsProfileHeader(ReadOnlySpan<byte> line)
        {
            int start = 0;
            int end = 0;
            while (end < line.Length && line[end] != (byte)',')
                end++;
            while (start < end && line[start] <= 32)
                start++;
            while (end > start && line[end - 1] <= 32)
                end--;

            int length = end - start;
            if (length == 4)
                return IsAsciiNoCase(line[start], (byte)'n') &&
                       IsAsciiNoCase(line[start + 1], (byte)'a') &&
                       IsAsciiNoCase(line[start + 2], (byte)'m') &&
                       IsAsciiNoCase(line[start + 3], (byte)'e');

            if (length == 7)
                return IsAsciiNoCase(line[start], (byte)'s') &&
                       IsAsciiNoCase(line[start + 1], (byte)'p') &&
                       IsAsciiNoCase(line[start + 2], (byte)'e') &&
                       IsAsciiNoCase(line[start + 3], (byte)'c') &&
                       IsAsciiNoCase(line[start + 4], (byte)'i') &&
                       IsAsciiNoCase(line[start + 5], (byte)'e') &&
                       IsAsciiNoCase(line[start + 6], (byte)'s');

            return false;
        }

        private static bool IsAsciiNoCase(byte value, byte lower)
        {
            if (value >= (byte)'A' && value <= (byte)'Z')
                value = (byte)(value + 32);
            return value == lower;
        }

        private static float ParseFloat(ReadOnlySpan<byte> bytes, float fallback)
        {
            if (bytes.Length <= 0)
                return fallback;

            int i = 0;
            float sign = 1f;
            if (bytes[0] == (byte)'-')
            {
                sign = -1f;
                i = 1;
            }

            float value = 0f;
            bool any = false;
            for (; i < bytes.Length; i++)
            {
                byte c = bytes[i];
                if (c == (byte)'.')
                    break;
                if (c < (byte)'0' || c > (byte)'9')
                    return any ? sign * value : fallback;
                value = (value * 10f) + (c - (byte)'0');
                any = true;
            }

            if (i < bytes.Length && bytes[i] == (byte)'.')
            {
                float scale = 0.1f;
                i++;
                for (; i < bytes.Length; i++)
                {
                    byte c = bytes[i];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;
                    value += (c - (byte)'0') * scale;
                    scale *= 0.1f;
                    any = true;
                }
            }

            float parsed = any ? sign * value : fallback;
            return math.isfinite(parsed) ? parsed : fallback;
        }

        private static uint Fnv1a(ReadOnlySpan<byte> bytes)
        {
            uint h = HashSeed;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte c = bytes[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                h ^= c;
                h *= HashPrime;
            }

            return h == 0u ? 1u : h;
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            hash ^= value;
            return hash * HashPrime;
        }

        private static void WriteUInt32LE(Span<byte> buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FinitePositive(float value, float fallback, float min, float max)
        {
            return math.clamp(math.isfinite(value) ? value : fallback, min, max);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ParasiteTargetDTO
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float ThermalSignature;
        [FieldOffset(16)] public float3 Velocity;
        [FieldOffset(28)] public float AttractionRadius;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ParasiteTargetCandidateDTO
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float ThermalSignature;
        [FieldOffset(28)] public float AttractionRadius;
        [FieldOffset(32)] public float3 Velocity;
        [FieldOffset(44)] public uint SourceHash;
        [FieldOffset(48)] public float Score;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint SourceIndex;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ParasiteGpuParticleDTO
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float Life01;
        [FieldOffset(16)] public float3 Velocity;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct ParasiteIndirectArgsDTO
    {
        [FieldOffset(0)] public uint VertexCountPerInstance;
        [FieldOffset(4)] public uint InstanceCount;
        [FieldOffset(8)] public uint StartVertex;
        [FieldOffset(12)] public uint StartInstance;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ParasiteFrameParamsDTO
    {
        [FieldOffset(0)] public float4 Frame0;
        [FieldOffset(16)] public float4 Frame1;
        [FieldOffset(32)] public float4 Frame2;
        [FieldOffset(48)] public float4 Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ParasiteSwarmTuningDTO
    {
        [FieldOffset(0)] public float ThermalAttractionMultiplier;
        [FieldOffset(4)] public float CurlNoiseFrequency;
        [FieldOffset(8)] public float SwarmMaxSpeed;
        [FieldOffset(12)] public float GlobalQualityOverride;
        [FieldOffset(16)] public float ParasiteAttractionThreshold;
        [FieldOffset(20)] public int MinParticleBudget;
        [FieldOffset(24)] public int MaxParticleBudget;
        [FieldOffset(28)] public float AttractionRadiusScale;
        [FieldOffset(32)] public float CurlStrength;
        [FieldOffset(36)] public float FlowFieldWeight;
        [FieldOffset(40)] public float AttachmentShellRadius;
        [FieldOffset(44)] public float TargetVelocityBlend;
        [FieldOffset(48)] public uint Version;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint ActiveProfileHash;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ParasiteBehaviorProfileDTO
    {
        [FieldOffset(0)] public uint SpeciesHash;
        [FieldOffset(4)] public float EmissionRate;
        [FieldOffset(8)] public float AttractionRadius;
        [FieldOffset(12)] public float NoiseMultiplier;
        [FieldOffset(16)] public float MaxSpeed;
        [FieldOffset(20)] public float ThermalBias;
        [FieldOffset(24)] public uint Version;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] private ulong _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SwarmTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint TargetCount;
        [FieldOffset(8)] public uint ParticleBudget;
        [FieldOffset(12)] public uint EstimatedGpuMicroseconds;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float MaxThermalSignature;
        [FieldOffset(24)] public uint StateHash;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float3 StrongestTargetLocal;
        [FieldOffset(44)] public uint OverflowCount;
        [FieldOffset(48)] public uint DumpSequence;
        [FieldOffset(52)] public uint ActiveProfileHash;
        [FieldOffset(56)] public uint RebaseFrame;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ParasiteScannerSummaryDTO
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint PrefabCount;
        [FieldOffset(8)] public uint ForbiddenParticleSystems;
        [FieldOffset(12)] public uint ExternalForceParticleSystems;
        [FieldOffset(16)] public uint CollisionParticleSystems;
        [FieldOffset(20)] public uint SwarmScriptHits;
        [FieldOffset(24)] public uint ReportHash;
        [FieldOffset(28)] public uint Flags;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockThermalTargetsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ParasiteTargetDTO> Targets;
        [NoAlias] public NativeArray<ParasiteTargetCandidateDTO> Candidates;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> TargetCount;

        public double3 CameraAup;
        public float PhaseRadians;
        public float GlobalQualityWeight;
        public ParasiteSwarmTuningDTO Tuning;

        public void Execute(int index)
        {
            int limit = math.min(ParasiteSwarmContracts.MaxTargetCount, math.min(Targets.Length, Candidates.Length));
            if (index == 0 && TargetCount.IsCreated && TargetCount.Length > 0)
                TargetCount[0] = limit;

            if (index >= limit)
                return;

            float q = ParasiteSwarmContracts.SmoothQuality01(GlobalQualityWeight);
            float angle = PhaseRadians * (0.73f + (index * 0.037f));
            float radius = math.lerp(3.5f, 18f, q) + (index * 0.17f);
            float3 local = new float3(
                ParasiteSwarmContracts.FastSinApprox(angle + index * 2.31f) * radius,
                ParasiteSwarmContracts.FastSinApprox((angle * 0.61f) + index) * 2.2f,
                ParasiteSwarmContracts.FastCosApprox(angle + index * 1.73f) * radius);
            float thermal = Tuning.ParasiteAttractionThreshold + 8f + ParasiteSwarmContracts.FastSinApprox(angle * 2.7f) * 3f + (index * 0.35f);
            float3 velocity = new float3(
                ParasiteSwarmContracts.FastCosApprox(angle),
                ParasiteSwarmContracts.FastSinApprox(angle * 0.5f) * 0.35f,
                -ParasiteSwarmContracts.FastSinApprox(angle)) * (2f + q * 6f);
            float attractionRadius = math.max(0.25f, (2.1f + q * 3.4f) * Tuning.AttractionRadiusScale);

            ParasiteTargetDTO target = new ParasiteTargetDTO
            {
                LocalPosition = local,
                ThermalSignature = thermal,
                Velocity = velocity,
                AttractionRadius = attractionRadius
            };
            Targets[index] = target;
            Candidates[index] = new ParasiteTargetCandidateDTO
            {
                Aup = CameraAup + new double3(local.x, local.y, local.z),
                ThermalSignature = thermal,
                AttractionRadius = attractionRadius,
                Velocity = velocity,
                Score = thermal + attractionRadius,
                SourceHash = 0xFACA313u ^ (uint)index,
                SourceIndex = (uint)index,
                Flags = 1u | ParasiteSwarmContracts.TelemetryFlagMockTargets
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ExtractParasiteTargetsJob : IJobParallelFor
    {
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<ParasiteTargetCandidateDTO> Candidates;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> CandidateCount;

        public int StagedCount;
        public double3 CameraAup;
        public ParasiteSwarmTuningDTO Tuning;

        public void Execute(int index)
        {
            int total = math.clamp(StagedCount, 0, Candidates.IsCreated ? Candidates.Length : 0);

            if (index == 0 && CandidateCount.IsCreated && CandidateCount.Length > 0)
                CandidateCount[0] = total;

            if (index >= total)
                return;

            void* inOutPtr = NativeArrayUnsafeUtility.GetUnsafePtr(Candidates);
            ref ParasiteTargetCandidateDTO candidate = ref UnsafeUtility.AsRef<ParasiteTargetCandidateDTO>((byte*)inOutPtr + (index * UnsafeUtility.SizeOf<ParasiteTargetCandidateDTO>()));
            candidate = ScoreStagedCandidate(in candidate);
        }

        private ParasiteTargetCandidateDTO ScoreStagedCandidate(in ParasiteTargetCandidateDTO staged)
        {
            if ((staged.Flags & 1u) == 0u ||
                !math.all(math.isfinite(staged.Aup)) ||
                !math.isfinite(staged.ThermalSignature) ||
                staged.ThermalSignature < Tuning.ParasiteAttractionThreshold)
            {
                return default;
            }

            double3 delta = staged.Aup - CameraAup;
            double distanceSq64 = math.lengthsq(delta);
            if (!math.all(math.isfinite(delta)) || distanceSq64 > 25000000.0)
                return default;

            float radius = math.max(0.25f, staged.AttractionRadius * Tuning.AttractionRadiusScale);
            float distanceSq = (float)math.max(0.0001, distanceSq64);
            float distance = distanceSq * math.rsqrt(distanceSq);
            float score = (staged.ThermalSignature * 3f) + radius - distance * 0.015f;
            ParasiteTargetCandidateDTO candidate = staged;
            candidate.AttractionRadius = radius;
            candidate.Score = score;
            candidate.Flags |= 1u;
            if (!math.all(math.isfinite(candidate.Velocity)))
                candidate.Velocity = default;
            return candidate;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct SelectTopParasiteTargetsJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<ParasiteTargetCandidateDTO> Candidates;
        [NoAlias] public NativeArray<ParasiteTargetDTO> Targets;
        [NoAlias] public NativeArray<int> TargetCount;

        public int CandidateCount;
        public double3 CameraAup;
        public ParasiteSwarmTuningDTO Tuning;

        public void Execute()
        {
            int targetLimit = math.min(ParasiteSwarmContracts.MaxTargetCount, Targets.Length);
            for (int i = 0; i < targetLimit; i++)
                Targets[i] = default;

            int selected = 0;
            int scanCount = math.clamp(CandidateCount, 0, Candidates.IsCreated ? Candidates.Length : 0);
            void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Candidates);
            float* selectedScores = stackalloc float[ParasiteSwarmContracts.MaxTargetCount];
            for (int i = 0; i < ParasiteSwarmContracts.MaxTargetCount; i++)
                selectedScores[i] = -3.402823e+38f;

            for (int i = 0; i < scanCount; i++)
            {
                ref readonly ParasiteTargetCandidateDTO candidate = ref UnsafeUtility.AsRef<ParasiteTargetCandidateDTO>((byte*)ptr + (i * UnsafeUtility.SizeOf<ParasiteTargetCandidateDTO>()));
                if ((candidate.Flags & 1u) == 0u ||
                    !math.isfinite(candidate.Score) ||
                    candidate.ThermalSignature < Tuning.ParasiteAttractionThreshold)
                    continue;

                ParasiteTargetDTO target = new ParasiteTargetDTO
                {
                    LocalPosition = Localize(in candidate),
                    ThermalSignature = candidate.ThermalSignature,
                    Velocity = candidate.Velocity,
                    AttractionRadius = math.max(0.25f, candidate.AttractionRadius)
                };

                int slot = selected;
                int compareLimit = math.min(selected, targetLimit);
                for (int j = 0; j < compareLimit; j++)
                {
                    if (candidate.Score > selectedScores[j])
                    {
                        slot = j;
                        break;
                    }
                }

                if (slot >= targetLimit)
                    continue;

                int shiftStart = math.min(selected, targetLimit - 1);
                for (int j = shiftStart; j > slot; j--)
                {
                    Targets[j] = Targets[j - 1];
                    selectedScores[j] = selectedScores[j - 1];
                }

                Targets[slot] = target;
                selectedScores[slot] = candidate.Score;
                selected = math.min(selected + 1, targetLimit);
            }

            if (TargetCount.IsCreated && TargetCount.Length > 0)
                TargetCount[0] = selected;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 Localize(in ParasiteTargetCandidateDTO candidate)
        {
            double3 local = candidate.Aup - CameraAup;
            return new float3((float)local.x, (float)local.y, (float)local.z);
        }
    }
}
