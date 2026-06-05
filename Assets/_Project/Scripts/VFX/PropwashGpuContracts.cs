using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.VFX.Wakes;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.VFX
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PropwashEventDTO
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float3 ThrustVector;
        [FieldOffset(24)] public float Intensity;
        [FieldOffset(28)] public float Radius;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct KinematicWakeSourceDTO
    {
        [FieldOffset(0)] public double3 EngineAup;
        [FieldOffset(24)] public float3 Forward;
        [FieldOffset(36)] public float EnginePower;
        [FieldOffset(40)] public float3 LinearVelocity;
        [FieldOffset(52)] public float TailSweepPower;
        [FieldOffset(56)] public float Radius;
        [FieldOffset(60)] public float QualityWeight;
        [FieldOffset(64)] public uint EntityId;
        [FieldOffset(68)] public uint Flags;
        [FieldOffset(72)] public float AgeSeconds;
        [FieldOffset(76)] public float Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PropwashRingCursorDTO
    {
        [FieldOffset(0)] public int WriteCursor;
        [FieldOffset(4)] public int EventCount;
        [FieldOffset(8)] public int DroppedCount;
        [FieldOffset(12)] public int LastFrame;
        [FieldOffset(16)] public uint StateHash;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public float Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PropwashTelemetryEntry
    {
        [FieldOffset(0)] public int Frame;
        [FieldOffset(4)] public int EventCount;
        [FieldOffset(8)] public int ParticleBudgetLimit;
        [FieldOffset(12)] public int OverflowCount;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float MaxIntensity;
        [FieldOffset(24)] public float EstimatedGpuMicroseconds;
        [FieldOffset(28)] public float SdfProximityMeters;
        [FieldOffset(32)] public float3 StrongestLocalPosition;
        [FieldOffset(44)] public uint StateHash;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint Cursor;
        [FieldOffset(56)] public uint ProfileHash;
        [FieldOffset(60)] public uint Pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PropwashGpuTuningDTO
    {
        [FieldOffset(0)] public float SiltProximityMeters;
        [FieldOffset(4)] public float CurlNoiseFrequency;
        [FieldOffset(8)] public float GlobalQualityWeightOverride;
        [FieldOffset(12)] public float MaxEventRadius;
        [FieldOffset(16)] public float BiomeTintR;
        [FieldOffset(20)] public float BiomeTintG;
        [FieldOffset(24)] public float BiomeTintB;
        [FieldOffset(28)] public uint Version;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PropwashWakeProfileDTO
    {
        [FieldOffset(0)] public uint EngineHash;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public float EmissionRate;
        [FieldOffset(12)] public float ParticleLifetime;
        [FieldOffset(16)] public float TurbulenceMultiplier;
        [FieldOffset(20)] public float RadiusMultiplier;
        [FieldOffset(24)] public float IntensityMultiplier;
        [FieldOffset(28)] public float SiltLift;
        [FieldOffset(32)] public float BiomeTintR;
        [FieldOffset(36)] public float BiomeTintG;
        [FieldOffset(40)] public float BiomeTintB;
        [FieldOffset(44)] public float CurlFrequency;
        [FieldOffset(48)] public float SpawnJitter;
        [FieldOffset(52)] public uint Reserved0;
        [FieldOffset(56)] public uint Reserved1;
        [FieldOffset(60)] public uint Reserved2;
    }

    public static class PropwashGpuContracts
    {
        public const int EventStrideBytes = 32;
        public const int KinematicSourceStrideBytes = 80;
        public const int WakeSourceStrideBytes = 128;
        public const int RingCursorStrideBytes = 32;
        public const int TelemetryEntryStrideBytes = 64;
        public const int TuningStrideBytes = 32;
        public const int WakeProfileStrideBytes = 64;
        public const int MockEventCount = 500;
        public const int EventRingCapacity = 512;
        public const int TelemetryCapacity = 300;
        public const int WakeProfileCapacity = 64;
        public const float MaxWakeProfileEmissionRate = 64f;
        public const byte WakeSourceVehicle = 2;
        public const byte WakeSourceApexPredator = 3;
        public const uint MockSourceFlag = 1u;
        public const uint VehicleWakeSourceFlag = 2u;
        public const uint WakeSourceBridgeFlag = 4u;
        public const uint LayoutHash = 0x53483237u;
        public const uint DefaultWakeProfileHash = 0x933B5BDEu;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_PROPWASH_GPU.h8dump";

        public static bool ValidateRuntimeLayouts()
        {
            return HasAlignedStride<PropwashEventDTO>(EventStrideBytes) &&
                HasAlignedStride<KinematicWakeSourceDTO>(KinematicSourceStrideBytes) &&
                HasAlignedStride<WakeSource>(WakeSourceStrideBytes) &&
                HasAlignedStride<PropwashRingCursorDTO>(RingCursorStrideBytes) &&
                HasAlignedStride<PropwashTelemetryEntry>(TelemetryEntryStrideBytes) &&
                HasAlignedStride<PropwashGpuTuningDTO>(TuningStrideBytes) &&
                HasAlignedStride<PropwashWakeProfileDTO>(WakeProfileStrideBytes);
        }

        private static bool HasAlignedStride<T>(int expectedStride) where T : struct
        {
            int actualStride = UnsafeUtility.SizeOf<T>();
            return actualStride == expectedStride && (actualStride & 7) == 0;
        }

        public static PropwashGpuTuningDTO CreateDefaultTuning()
        {
            return new PropwashGpuTuningDTO
            {
                SiltProximityMeters = 1.8f,
                CurlNoiseFrequency = 0.215f,
                GlobalQualityWeightOverride = -1f,
                MaxEventRadius = 12f,
                BiomeTintR = 0.46f,
                BiomeTintG = 0.42f,
                BiomeTintB = 0.35f,
                Version = 1u
            };
        }

        public static PropwashWakeProfileDTO CreateDefaultWakeProfile()
        {
            return new PropwashWakeProfileDTO
            {
                EngineHash = DefaultWakeProfileHash,
                Version = 1u,
                EmissionRate = 1f,
                ParticleLifetime = 0.85f,
                TurbulenceMultiplier = 1f,
                RadiusMultiplier = 1f,
                IntensityMultiplier = 1f,
                SiltLift = 0.08f,
                BiomeTintR = 0.46f,
                BiomeTintG = 0.42f,
                BiomeTintB = 0.35f,
                CurlFrequency = 0.215f,
                SpawnJitter = 0.18f,
                Reserved0 = 0u,
                Reserved1 = 0u,
                Reserved2 = 0u
            };
        }

        public static int ResolveParticleBudget(float globalQualityWeight)
        {
            float q = math.saturate(globalQualityWeight);
            float curved = q * q * (3f - 2f * q);
            return math.clamp((int)math.round(math.lerp(
                VfxComputeParticleBudgetCatalog.MinimumQualityMarineSnowCount,
                VfxComputeParticleBudgetCatalog.OverkillQualityMarineSnowCount,
                curved)), 64, VfxComputeParticleBudgetCatalog.OverkillQualityMarineSnowCount);
        }

        public static uint HashState(int frame, int eventCount, float qualityWeight, uint profileHash)
        {
            uint hash = 2166136261u;
            hash = (hash ^ unchecked((uint)frame)) * 16777619u;
            hash = (hash ^ unchecked((uint)eventCount)) * 16777619u;
            hash = (hash ^ math.asuint(qualityWeight)) * 16777619u;
            hash = (hash ^ profileHash) * 16777619u;
            return hash == 0u ? LayoutHash : hash;
        }
    }

    #if UNITY_EDITOR
    public static class PropwashGpuProfileCsvParser
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const uint SiltProximityHash = 0xDBBF2DF6u;
        private const uint CurlNoiseFrequencyHash = 0xA0AE9CFBu;
        private const uint GlobalQualityWeightHash = 0xB00FB719u;
        private const uint QualityOverrideHash = 0xBD9CD603u;
        private const uint MaxEventRadiusHash = 0x4034C951u;
        private const uint BiomeTintRHash = 0x0ADB3688u;
        private const uint BiomeTintGHash = 0xFFDB2537u;
        private const uint BiomeTintBHash = 0xFADB1D58u;

        public static bool TryParse(ReadOnlySpan<byte> bytes, ref PropwashGpuTuningDTO tuning, out uint fileHash)
        {
            fileHash = HashBytes(bytes);
            bool changed = false;
            int cursor = 0;
            while (cursor < bytes.Length)
            {
                ReadOnlySpan<byte> line = ReadLine(bytes, ref cursor);
                changed |= TryApplyLine(line, ref tuning);
            }

            if (changed)
            {
                tuning.Version = tuning.Version == uint.MaxValue ? 1u : tuning.Version + 1u;
            }

            return changed;
        }

        public static bool TryParseWakeProfiles(
            ReadOnlySpan<byte> bytes,
            NativeArray<PropwashWakeProfileDTO> profiles,
            out int profileCount,
            out uint fileHash)
        {
            fileHash = HashBytes(bytes);
            profileCount = 0;
            if (!profiles.IsCreated || profiles.Length <= 0)
                return false;

            int cursor = 0;
            while (cursor < bytes.Length && profileCount < profiles.Length)
            {
                ReadOnlySpan<byte> line = ReadLine(bytes, ref cursor);
                TryApplyWakeProfileLine(line, profiles, ref profileCount);
            }

            return profileCount > 0;
        }

        private static bool TryApplyLine(ReadOnlySpan<byte> line, ref PropwashGpuTuningDTO tuning)
        {
            line = Trim(line);
            if (line.Length <= 0 || line[0] == (byte)'#')
                return false;

            int separator = IndexOfSeparator(line);
            if (separator <= 0)
                return false;

            ReadOnlySpan<byte> key = Trim(line.Slice(0, separator));
            ReadOnlySpan<byte> valueSpan = Trim(line.Slice(separator + 1));
            if (!TryParseFloat(valueSpan, out float value))
                return false;

            switch (HashLowerAscii(key))
            {
                case SiltProximityHash:
                    tuning.SiltProximityMeters = math.clamp(value, 0.05f, 8f);
                    return true;
                case CurlNoiseFrequencyHash:
                    tuning.CurlNoiseFrequency = math.clamp(value, 0.01f, 2f);
                    return true;
                case GlobalQualityWeightHash:
                case QualityOverrideHash:
                    tuning.GlobalQualityWeightOverride = math.clamp(value, -1f, 1f);
                    return true;
                case MaxEventRadiusHash:
                    tuning.MaxEventRadius = math.clamp(value, 0.25f, 32f);
                    return true;
                case BiomeTintRHash:
                    tuning.BiomeTintR = math.saturate(value);
                    return true;
                case BiomeTintGHash:
                    tuning.BiomeTintG = math.saturate(value);
                    return true;
                case BiomeTintBHash:
                    tuning.BiomeTintB = math.saturate(value);
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryApplyWakeProfileLine(
            ReadOnlySpan<byte> line,
            NativeArray<PropwashWakeProfileDTO> profiles,
            ref int profileCount)
        {
            line = Trim(line);
            if (line.Length <= 0 || line[0] == (byte)'#' || profileCount >= profiles.Length)
                return false;

            int cursor = 0;
            ReadOnlySpan<byte> engineName = Trim(ReadCsvToken(line, ref cursor));
            if (engineName.Length <= 0)
                return false;

            PropwashWakeProfileDTO profile = PropwashGpuContracts.CreateDefaultWakeProfile();
            profile.EngineHash = HashLowerAscii(engineName);
            profile.Version = 1u;

            if (!TryParseNextFloat(line, ref cursor, out profile.EmissionRate))
                return false;

            if (!TryParseOptionalFloat(line, ref cursor, ref profile.ParticleLifetime) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.TurbulenceMultiplier) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.RadiusMultiplier) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.IntensityMultiplier) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.SiltLift) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.BiomeTintR) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.BiomeTintG) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.BiomeTintB) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.CurlFrequency) ||
                !TryParseOptionalFloat(line, ref cursor, ref profile.SpawnJitter))
                return false;

            profile.EmissionRate = math.clamp(profile.EmissionRate, 0f, 64f);
            profile.ParticleLifetime = math.clamp(profile.ParticleLifetime, 0.05f, 12f);
            profile.TurbulenceMultiplier = math.clamp(profile.TurbulenceMultiplier, 0f, 8f);
            profile.RadiusMultiplier = math.clamp(profile.RadiusMultiplier, 0.05f, 8f);
            profile.IntensityMultiplier = math.clamp(profile.IntensityMultiplier, 0f, 8f);
            profile.SiltLift = math.clamp(profile.SiltLift, 0f, 4f);
            profile.BiomeTintR = math.saturate(profile.BiomeTintR);
            profile.BiomeTintG = math.saturate(profile.BiomeTintG);
            profile.BiomeTintB = math.saturate(profile.BiomeTintB);
            profile.CurlFrequency = math.clamp(profile.CurlFrequency, 0.01f, 4f);
            profile.SpawnJitter = math.clamp(profile.SpawnJitter, 0f, 2f);

            profiles[profileCount] = profile;
            profileCount++;
            return true;
        }

        private static ReadOnlySpan<byte> ReadCsvToken(ReadOnlySpan<byte> line, ref int cursor)
        {
            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;

            return line.Slice(start, end - start);
        }

        private static bool TryParseNextFloat(ReadOnlySpan<byte> line, ref int cursor, out float value)
        {
            value = 0f;
            if (cursor >= line.Length)
                return false;

            ReadOnlySpan<byte> token = Trim(ReadCsvToken(line, ref cursor));
            return TryParseFloat(token, out value);
        }

        private static bool TryParseOptionalFloat(ReadOnlySpan<byte> line, ref int cursor, ref float value)
        {
            if (cursor >= line.Length)
                return true;

            ReadOnlySpan<byte> token = Trim(ReadCsvToken(line, ref cursor));
            if (token.Length <= 0)
                return true;

            if (!TryParseFloat(token, out float parsed))
                return false;

            value = parsed;
            return true;
        }

        private static ReadOnlySpan<byte> ReadLine(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            int start = cursor;
            while (cursor < bytes.Length && bytes[cursor] != (byte)'\n')
                cursor++;

            int end = cursor;
            if (cursor < bytes.Length && bytes[cursor] == (byte)'\n')
                cursor++;

            return bytes.Slice(start, end - start);
        }

        private static int IndexOfSeparator(ReadOnlySpan<byte> line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                byte b = line[i];
                if (b == (byte)',' || b == (byte)'=')
                    return i;
            }

            return -1;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start <= end && IsAsciiWhitespace(span[start]))
                start++;
            while (end >= start && IsAsciiWhitespace(span[end]))
                end--;
            return start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static uint HashBytes(ReadOnlySpan<byte> bytes)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash ^ bytes[i]) * FnvPrime;
            return hash == 0u ? FnvOffset : hash;
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> bytes)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash = (hash ^ b) * FnvPrime;
            }

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

            if (!hasDigit)
                return false;

            if (index != bytes.Length)
                return false;

            value = integer + fraction / divisor;
            if (negative)
                value = -value;
            return math.isfinite(value);
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' ||
                value == (byte)'\t' ||
                value == (byte)'\r' ||
                value == (byte)'\n';
        }
    }
    #endif

    public static unsafe class PropwashTelemetryDump
    {
        private const string PayloadOwner = nameof(PropwashTelemetryDump);
        private const string PayloadLabel = "PropwashTelemetryDumpPayload";

        public static bool TryWrite(
            string projectRoot,
            NativeArray<PropwashTelemetryEntry>.ReadOnly telemetryRing,
            int writeIndex,
            int writtenCount)
        {
            int count = telemetryRing.IsCreated
                ? math.clamp(writtenCount, 0, math.min(telemetryRing.Length, PropwashGpuContracts.TelemetryCapacity))
                : 0;
            if (count <= 0)
                return false;

            NativeArray<byte> payload = default;
            try
            {
                int byteCount = 16 + count * PropwashGpuContracts.TelemetryEntryStrideBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(byteCount, PayloadOwner, PayloadLabel);
                byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                WriteUInt32LittleEndian(payloadPtr, 0, PropwashGpuContracts.LayoutHash);
                WriteUInt32LittleEndian(payloadPtr, 4, unchecked((uint)PropwashGpuContracts.TelemetryCapacity));
                WriteUInt32LittleEndian(payloadPtr, 8, unchecked((uint)PropwashGpuContracts.TelemetryEntryStrideBytes));
                WriteUInt32LittleEndian(payloadPtr, 12, unchecked((uint)math.max(0, writtenCount)));

                int readIndex = count >= PropwashGpuContracts.TelemetryCapacity ? WrapIndex(writeIndex, PropwashGpuContracts.TelemetryCapacity) : 0;
                byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
                int firstCount = math.min(count, PropwashGpuContracts.TelemetryCapacity - readIndex);
                UnsafeUtility.MemCpy(
                    payloadPtr + 16,
                    basePtr + readIndex * PropwashGpuContracts.TelemetryEntryStrideBytes,
                    firstCount * PropwashGpuContracts.TelemetryEntryStrideBytes);
                int secondCount = count - firstCount;
                if (secondCount > 0)
                {
                    UnsafeUtility.MemCpy(
                        payloadPtr + 16 + firstCount * PropwashGpuContracts.TelemetryEntryStrideBytes,
                        basePtr,
                        secondCount * PropwashGpuContracts.TelemetryEntryStrideBytes);
                }

                string path = string.IsNullOrWhiteSpace(projectRoot)
                    ? PropwashGpuContracts.DumpRelativePath
                    : Path.Combine(projectRoot, PropwashGpuContracts.DumpRelativePath);
                return NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(ref payload, PayloadOwner, PayloadLabel);
            }
        }

        private static int WrapIndex(int value, int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            int wrapped = value % safeCapacity;
            return wrapped < 0 ? wrapped + safeCapacity : wrapped;
        }

        private static void WriteUInt32LittleEndian(byte* target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }
    }
}
