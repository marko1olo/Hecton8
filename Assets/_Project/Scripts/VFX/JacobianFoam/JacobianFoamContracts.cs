using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.VFX
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FoamComputeParamsDTO
    {
        [FieldOffset(0)] public float4 AdvectionVectors;
        [FieldOffset(16)] public float4 DecayAndIntensity;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FoamWakeImpactDTO
    {
        [FieldOffset(0)] public float4 LocalPositionRadius;
        [FieldOffset(16)] public float4 IntensityAgeFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FoamTuningDTO
    {
        [FieldOffset(0)] public float PinchThreshold;
        [FieldOffset(4)] public float DecayRate;
        [FieldOffset(8)] public float ShorelineDepthFade;
        [FieldOffset(12)] public float AdvectionSpeed;
        [FieldOffset(16)] public float WindX;
        [FieldOffset(20)] public float WindZ;
        [FieldOffset(24)] public float GlobalQualityWeightOverride;
        [FieldOffset(28)] public float TextureWorldSizeMeters;
        [FieldOffset(32)] public float WakeGain;
        [FieldOffset(36)] public float Intensity;
        [FieldOffset(40)] public float MinResolution;
        [FieldOffset(44)] public float MaxResolution;
        [FieldOffset(48)] public uint Version;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float Pad0;
        [FieldOffset(60)] public float Pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FoamRenderTelemetryEntry
    {
        [FieldOffset(0)] public int Frame;
        [FieldOffset(4)] public int Resolution;
        [FieldOffset(8)] public int WakeCount;
        [FieldOffset(12)] public int DispatchGroups;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float ResolutionScale;
        [FieldOffset(24)] public float EstimatedGpuMicroseconds;
        [FieldOffset(28)] public float ShorelineContribution;
        [FieldOffset(32)] public float2 ScrollOffset;
        [FieldOffset(40)] public uint StateHash;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint Cursor;
        [FieldOffset(52)] public uint ProfileHash;
        [FieldOffset(56)] public float DecayRate;
        [FieldOffset(60)] public uint Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FoamAestheticProfileDTO
    {
        [FieldOffset(0)] public uint NameHash;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public float PinchThreshold;
        [FieldOffset(12)] public float DecayRate;
        [FieldOffset(16)] public float ShorelineDepthFade;
        [FieldOffset(20)] public float AdvectionSpeed;
        [FieldOffset(24)] public float WindX;
        [FieldOffset(28)] public float WindZ;
        [FieldOffset(32)] public float WakeGain;
        [FieldOffset(36)] public float Intensity;
        [FieldOffset(40)] public float FoamLifetimeBias;
        [FieldOffset(44)] public float CrestSharpness;
        [FieldOffset(48)] public float MinimumQualityResolutionBias;
        [FieldOffset(52)] public float MaximumQualityResolutionBias;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Reserved0;
    }

    public static unsafe class JacobianFoamContracts
    {
        public const int ParamsStrideBytes = 32;
        public const int WakeImpactStrideBytes = 32;
        public const int TuningStrideBytes = 64;
        public const int TelemetryEntryStrideBytes = 64;
        public const int ProfileStrideBytes = 64;
        public const int TelemetryCapacity = 300;
        public const int WakeImpactCapacity = 64;
        public const int ProfileCapacity = 32;
        public const int MaxDispatchGroupsPerDimension = 65535;
        public const int CsvScratchBytes = 16 * 1024;
        public const uint LayoutHash = 0x53463236u;
        public const uint DefaultProfileHash = 0xD2E69F10u;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_266.bin";

        public static bool ValidateRuntimeLayouts()
        {
            return UnsafeUtility.SizeOf<FoamComputeParamsDTO>() == ParamsStrideBytes &&
                UnsafeUtility.SizeOf<FoamWakeImpactDTO>() == WakeImpactStrideBytes &&
                UnsafeUtility.SizeOf<FoamTuningDTO>() == TuningStrideBytes &&
                UnsafeUtility.SizeOf<FoamRenderTelemetryEntry>() == TelemetryEntryStrideBytes &&
                UnsafeUtility.SizeOf<FoamAestheticProfileDTO>() == ProfileStrideBytes;
        }

        public static FoamTuningDTO CreateDefaultTuning()
        {
            return new FoamTuningDTO
            {
                PinchThreshold = 0.82f,
                DecayRate = 0.42f,
                ShorelineDepthFade = 0.065f,
                AdvectionSpeed = 9.5f,
                WindX = 0.78f,
                WindZ = 0.36f,
                GlobalQualityWeightOverride = -1f,
                TextureWorldSizeMeters = 512f,
                WakeGain = 1.35f,
                Intensity = 1.15f,
                MinResolution = 512f,
                MaxResolution = 2048f,
                Version = 1u,
                Flags = 1u,
                Pad0 = 0f,
                Pad1 = 0f
            };
        }

        public static FoamAestheticProfileDTO CreateDefaultProfile()
        {
            return new FoamAestheticProfileDTO
            {
                NameHash = DefaultProfileHash,
                Version = 1u,
                PinchThreshold = 0.82f,
                DecayRate = 0.42f,
                ShorelineDepthFade = 0.065f,
                AdvectionSpeed = 9.5f,
                WindX = 0.78f,
                WindZ = 0.36f,
                WakeGain = 1.35f,
                Intensity = 1.15f,
                FoamLifetimeBias = 1f,
                CrestSharpness = 1f,
                MinimumQualityResolutionBias = 1f,
                MaximumQualityResolutionBias = 1f,
                Flags = 1u,
                Reserved0 = 0u
            };
        }

        public static FoamComputeParamsDTO BuildParams(in FoamTuningDTO tuning, float qualityWeight, float deltaTime, float2 scrollOffset)
        {
            float2 wind = new float2(tuning.WindX, tuning.WindZ);
            float windLengthSq = math.lengthsq(wind);
            wind = windLengthSq > 0.000001f ? wind * math.rsqrt(windLengthSq) : new float2(1f, 0f);
            float quality = math.saturate(qualityWeight);
            float advectionSpeed = SanitizeNonNegative(tuning.AdvectionSpeed, 8f) * math.lerp(0.35f, 1.25f, quality);

            return new FoamComputeParamsDTO
            {
                AdvectionVectors = new float4(wind.x * advectionSpeed, wind.y * advectionSpeed, scrollOffset.x, scrollOffset.y),
                DecayAndIntensity = new float4(
                    SanitizeNonNegative(tuning.DecayRate, 0.42f),
                    SanitizeNonNegative(tuning.Intensity, 1f),
                    math.clamp(SanitizeFinite(tuning.PinchThreshold, 0.82f), 0.05f, 1.5f),
                    math.clamp(deltaTime, 0f, 0.1f))
            };
        }

        public static int ResolveFoamResolution(float globalQualityWeight, int minResolution, int maxResolution)
        {
            int minSafe = math.max(256, Align64(minResolution));
            int maxSafe = math.max(minSafe, Align64(maxResolution));
            float q = math.saturate(globalQualityWeight);
            float curved = q * q * (3f - 2f * q);
            float resolution = math.lerp(minSafe, maxSafe, curved);
            return math.clamp(Align64((int)math.round(resolution)), minSafe, maxSafe);
        }

        public static int ResolveDispatchGroups(int resolution, int threadGroupSize)
        {
            if (resolution <= 0 || threadGroupSize <= 0)
                return 0;

            long groups = ((long)resolution + threadGroupSize - 1L) / threadGroupSize;
            return groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
        }

        public static float2 ResolveWrappedScrollOffset(double2 cameraAupXz, float textureWorldSizeMeters)
        {
            double size = math.max(1.0, textureWorldSizeMeters);
            double x = cameraAupXz.x - math.floor(cameraAupXz.x / size) * size;
            double z = cameraAupXz.y - math.floor(cameraAupXz.y / size) * size;
            return new float2((float)x, (float)z);
        }

        public static uint HashState(int frame, int resolution, int wakeCount, float qualityWeight, float2 scrollOffset, uint profileHash)
        {
            uint hash = 2166136261u;
            hash = (hash ^ unchecked((uint)frame)) * 16777619u;
            hash = (hash ^ unchecked((uint)resolution)) * 16777619u;
            hash = (hash ^ unchecked((uint)wakeCount)) * 16777619u;
            hash = (hash ^ math.asuint(qualityWeight)) * 16777619u;
            hash = (hash ^ math.asuint(scrollOffset.x)) * 16777619u;
            hash = (hash ^ math.asuint(scrollOffset.y)) * 16777619u;
            hash = (hash ^ profileHash) * 16777619u;
            return hash == 0u ? LayoutHash : hash;
        }

        public static ref FoamComputeParamsDTO MutableParamsRef(NativeArray<FoamComputeParamsDTO> buffer)
        {
            return ref UnsafeUtility.AsRef<FoamComputeParamsDTO>(NativeArrayUnsafeUtility.GetUnsafePtr(buffer));
        }

        public static bool EnsureVaultBuffers(IDataVault vault)
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            vault.EnsureGenerationHandle<FoamComputeParamsDTO>(BufferID.JacobianFoamParams, 1, SystemID.Vfx, NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<FoamTuningDTO>(BufferID.JacobianFoamTuning, 1, SystemID.Vfx, NativeArrayOptions.ClearMemory);
            vault.EnsureGenerationHandle<FoamWakeImpactDTO>(BufferID.JacobianFoamWakeImpacts, WakeImpactCapacity, SystemID.Vfx, NativeArrayOptions.ClearMemory);
            vault.EnsureGenerationHandle<FoamRenderTelemetryEntry>(BufferID.JacobianFoamTelemetryRing, TelemetryCapacity, SystemID.Vfx, NativeArrayOptions.ClearMemory);
            vault.EnsureGenerationHandle<FoamAestheticProfileDTO>(BufferID.JacobianFoamProfiles, ProfileCapacity, SystemID.Vfx, NativeArrayOptions.ClearMemory);
            vault.EnsureGenerationHandle<byte>(BufferID.JacobianFoamCsvScratch, CsvScratchBytes, SystemID.Vfx, NativeArrayOptions.UninitializedMemory);
            return true;
        }

        public static float EstimateGpuMicroseconds(int resolution, int wakeCount, float qualityWeight)
        {
            float pixels = resolution * resolution;
            float wakeFactor = 1f + math.saturate(wakeCount * (1f / WakeImpactCapacity)) * 0.45f;
            float qualityFactor = math.lerp(0.55f, 1.35f, math.saturate(qualityWeight));
            return pixels * (0.000135f * wakeFactor * qualityFactor);
        }

        public static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        public static float SanitizeNonNegative(float value, float fallback)
        {
            return math.isfinite(value) ? math.max(0f, value) : fallback;
        }

        private static int Align64(int value)
        {
            int safe = math.max(1, value);
            return ((safe + 63) / 64) * 64;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CopyFoamParamsToMappedBufferJob : IJob
    {
        [ReadOnly] [NoAlias] public NativeArray<FoamComputeParamsDTO> Source;
        [NoAlias] public NativeArray<FoamComputeParamsDTO> Destination;

        public void Execute()
        {
            if (!Source.IsCreated || !Destination.IsCreated || Source.Length <= 0 || Destination.Length <= 0)
                return;

            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Source);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafePtr(Destination);
            UnsafeUtility.MemCpy(destinationPtr, sourcePtr, JacobianFoamContracts.ParamsStrideBytes);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CopyFoamWakesToMappedBufferJob : IJob
    {
        [ReadOnly] [NoAlias] public NativeArray<FoamWakeImpactDTO> Source;
        [NoAlias] public NativeArray<FoamWakeImpactDTO> Destination;
        public int Count;

        public void Execute()
        {
            if (!Destination.IsCreated || Destination.Length <= 0)
                return;

            int destinationCount = math.min(Destination.Length, JacobianFoamContracts.WakeImpactCapacity);
            int sourceCount = Source.IsCreated ? math.min(Source.Length, destinationCount) : 0;
            int copyCount = math.clamp(Count, 0, sourceCount);
            for (int i = 0; i < copyCount; i++)
                Destination[i] = Source[i];

            for (int i = copyCount; i < destinationCount; i++)
                Destination[i] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockStormStateJob : IJob
    {
        [NoAlias] public NativeArray<FoamComputeParamsDTO> Params;
        [NoAlias] public NativeArray<FoamTuningDTO> Tuning;
        [NoAlias] public NativeArray<FoamWakeImpactDTO> WakeImpacts;
        public float TimeSeconds;
        public float GlobalQualityWeight;
        public float DeltaTime;
        public float2 ScrollOffset;

        public void Execute()
        {
            if (!Params.IsCreated || Params.Length <= 0)
                return;

            FoamTuningDTO tuning = Tuning.IsCreated && Tuning.Length > 0 && Tuning[0].Version != 0u
                ? Tuning[0]
                : JacobianFoamContracts.CreateDefaultTuning();

            tuning.WindX = 0.92f;
            tuning.WindZ = 0.38f;
            tuning.AdvectionSpeed = math.lerp(7.5f, 18.5f, math.saturate(GlobalQualityWeight));
            tuning.Intensity = math.lerp(0.85f, 2.45f, math.saturate(GlobalQualityWeight));
            tuning.PinchThreshold = math.lerp(0.90f, 0.68f, math.saturate(GlobalQualityWeight));
            tuning.Version = tuning.Version == uint.MaxValue ? 1u : tuning.Version + 1u;

            ref FoamComputeParamsDTO paramRef = ref JacobianFoamContracts.MutableParamsRef(Params);
            paramRef = JacobianFoamContracts.BuildParams(in tuning, GlobalQualityWeight, DeltaTime, ScrollOffset);

            if (Tuning.IsCreated && Tuning.Length > 0)
                Tuning[0] = tuning;

            if (!WakeImpacts.IsCreated)
                return;

            int count = math.min(WakeImpacts.Length, JacobianFoamContracts.WakeImpactCapacity);
            for (int i = 0; i < count; i++)
            {
                float lane = (i + 1f) * math.rcp(math.max(1f, count));
                float phase = TimeSeconds * (0.17f + lane * 0.31f) + lane * 17f;
                float radius = math.lerp(2.5f, 24f, lane) * math.lerp(0.65f, 1.4f, math.saturate(GlobalQualityWeight));
                float2 pos = new float2(TriangleSigned(phase) * 210f, TriangleSigned(phase * 0.73f + 0.25f) * 210f);
                float intensity = math.saturate(1f - lane * 0.55f) * tuning.WakeGain;
                WakeImpacts[i] = new FoamWakeImpactDTO
                {
                    LocalPositionRadius = new float4(pos.x, 0f, pos.y, radius),
                    IntensityAgeFlags = new float4(intensity, TimeSeconds, 1f, 0f)
                };
            }
        }

        private static float TriangleSigned(float phase)
        {
            float t = math.frac(phase);
            return (math.abs(t * 2f - 1f) * 2f) - 1f;
        }
    }

    #if UNITY_EDITOR
    public static class FoamAestheticProfileCsvParser
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static int ParseProfiles(ReadOnlySpan<byte> bytes, NativeArray<FoamAestheticProfileDTO> profiles)
        {
            if (!profiles.IsCreated || profiles.Length <= 0 || bytes.Length <= 0)
                return 0;

            int count = 0;
            int lineStart = 0;
            bool headerSkipped = false;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != (byte)'\n')
                    continue;

                ReadOnlySpan<byte> line = Trim(bytes.Slice(lineStart, i - lineStart));
                lineStart = i + 1;
                if (line.Length <= 0 || line[0] == (byte)'#')
                    continue;

                if (!headerSkipped && StartsWithNameHeader(line))
                {
                    headerSkipped = true;
                    continue;
                }

                if (TryParseProfile(line, out FoamAestheticProfileDTO profile))
                {
                    profiles[count] = profile;
                    count++;
                    if (count >= profiles.Length)
                        break;
                }
            }

            return count;
        }

        private static bool TryParseProfile(ReadOnlySpan<byte> line, out FoamAestheticProfileDTO profile)
        {
            profile = JacobianFoamContracts.CreateDefaultProfile();
            Span<FieldSlice> fields = stackalloc FieldSlice[8];
            if (SliceCsvLine(line, fields) < 8)
                return false;

            profile.NameHash = HashLowerAscii(fields[0].Slice(line));
            profile.Version = 1u;
            if (!TryParseFloat(fields[1].Slice(line), out profile.PinchThreshold) ||
                !TryParseFloat(fields[2].Slice(line), out profile.DecayRate) ||
                !TryParseFloat(fields[3].Slice(line), out profile.ShorelineDepthFade) ||
                !TryParseFloat(fields[4].Slice(line), out profile.AdvectionSpeed) ||
                !TryParseFloat(fields[5].Slice(line), out profile.WindX) ||
                !TryParseFloat(fields[6].Slice(line), out profile.WindZ) ||
                !TryParseFloat(fields[7].Slice(line), out profile.Intensity))
            {
                return false;
            }

            profile.WakeGain = math.max(0.1f, profile.Intensity);
            profile.FoamLifetimeBias = 1f;
            profile.CrestSharpness = 1f;
            profile.MinimumQualityResolutionBias = 1f;
            profile.MaximumQualityResolutionBias = 1f;
            profile.Flags = 1u;
            return profile.NameHash != 0u;
        }

        private static int SliceCsvLine(ReadOnlySpan<byte> line, Span<FieldSlice> fields)
        {
            int count = 0;
            int start = 0;
            for (int i = 0; i <= line.Length; i++)
            {
                if (i < line.Length && line[i] != (byte)',')
                    continue;

                if (count < fields.Length)
                    fields[count] = new FieldSlice(start, i);
                count++;
                start = i + 1;
            }

            return count;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> bytes)
        {
            int start = 0;
            int end = bytes.Length - 1;
            while (start < bytes.Length && IsAsciiWhitespace(bytes[start]))
                start++;
            while (end >= start && IsAsciiWhitespace(bytes[end]))
                end--;
            return end < start ? ReadOnlySpan<byte>.Empty : bytes.Slice(start, end - start + 1);
        }

        private static bool StartsWithNameHeader(ReadOnlySpan<byte> line)
        {
            line = Trim(line);
            return line.Length >= 4 &&
                ToLower(line[0]) == (byte)'n' &&
                ToLower(line[1]) == (byte)'a' &&
                ToLower(line[2]) == (byte)'m' &&
                ToLower(line[3]) == (byte)'e';
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> bytes)
        {
            bytes = Trim(bytes);
            uint hash = FnvOffset;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash ^ ToLower(bytes[i])) * FnvPrime;
            return hash == 0u ? FnvOffset : hash;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            bytes = Trim(bytes);
            if (bytes.Length <= 0)
                return false;

            int index = 0;
            bool negative = false;
            if (bytes[index] == (byte)'-')
            {
                negative = true;
                index++;
            }
            else if (bytes[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (index < bytes.Length)
            {
                byte b = bytes[index];
                if (b < (byte)'0' || b > (byte)'9')
                    break;
                integer = integer * 10f + (b - (byte)'0');
                hasDigit = true;
                index++;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < bytes.Length && bytes[index] == (byte)'.')
            {
                index++;
                while (index < bytes.Length)
                {
                    byte b = bytes[index];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;
                    fraction = fraction * 10f + (b - (byte)'0');
                    divisor *= 10f;
                    hasDigit = true;
                    index++;
                }
            }

            if (!hasDigit || index != bytes.Length)
                return false;

            value = negative ? -(integer + fraction / divisor) : integer + fraction / divisor;
            return math.isfinite(value);
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }

        private static byte ToLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private readonly struct FieldSlice
        {
            private readonly int _start;
            private readonly int _length;

            public FieldSlice(int start, int end)
            {
                _start = start;
                _length = math.max(0, end - start);
            }

            public ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> source)
            {
                return source.Slice(_start, _length);
            }
        }
    }
    #endif

    public static unsafe class FoamTelemetryDump
    {
        public static bool TryWrite(string projectRoot, NativeArray<FoamRenderTelemetryEntry> telemetryRing, int writeIndex, int writtenCount)
        {
            if (string.IsNullOrEmpty(projectRoot) || !telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return false;

            int count = math.clamp(writtenCount, 0, math.min(telemetryRing.Length, JacobianFoamContracts.TelemetryCapacity));
            if (count <= 0)
                return false;

            try
            {
                string path = Path.Combine(projectRoot, JacobianFoamContracts.DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                int start = count >= telemetryRing.Length ? WrapIndex(writeIndex, telemetryRing.Length) : 0;
                byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
                int stride = JacobianFoamContracts.TelemetryEntryStrideBytes;
                int firstCount = math.min(count, telemetryRing.Length - start);
                stream.Write(new ReadOnlySpan<byte>(basePtr + start * stride, firstCount * stride));
                int secondCount = count - firstCount;
                if (secondCount > 0)
                    stream.Write(new ReadOnlySpan<byte>(basePtr, secondCount * stride));

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static int WrapIndex(int value, int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            int wrapped = value % safeCapacity;
            return wrapped < 0 ? wrapped + safeCapacity : wrapped;
        }
    }
}
