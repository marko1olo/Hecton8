using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Core
{
    public static class HapticSynthesisFaultFlags
    {
        public const uint None = 0u;
        public const uint NanSanitized = 1u << 0;
        public const uint BudgetExceeded = 1u << 1;
        public const uint PulseOverflow = 1u << 2;
        public const uint MissingPlayerAup = 1u << 3;
        public const uint MockStormActive = 1u << 4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HapticPhysicalImpulseDTO
    {
        [FieldOffset(0)] public double3 ImpactAup;
        [FieldOffset(24)] public float Magnitude;
        [FieldOffset(28)] public float Sharpness01;
        [FieldOffset(32)] public uint MaterialHash;
        [FieldOffset(36)] public uint SourceHash;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public byte Channel;
        [FieldOffset(45)] public byte Flags;
        [FieldOffset(46)] private ushort _padTail0;
        [FieldOffset(48)] private ulong _padTail1;
        [FieldOffset(56)] private ulong _padTail2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HapticProfileDTO
    {
        [FieldOffset(0)] public uint MaterialHash;
        [FieldOffset(4)] public float LowGain;
        [FieldOffset(8)] public float HighGain;
        [FieldOffset(12)] public float DurationScale;
        [FieldOffset(16)] public float SharpnessBias;
        [FieldOffset(20)] public float DistanceBias;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] private uint _padTail0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HapticTuningDTO
    {
        [FieldOffset(0)] public float DistanceAttenuationCurve;
        [FieldOffset(4)] public float GlobalRumbleMultiplier;
        [FieldOffset(8)] public float MaxMotorAmplitude;
        [FieldOffset(12)] public float MicroscopicThreshold;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float TickIntervalSeconds;
        [FieldOffset(24)] public uint ProfileCount;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HapticTelemetryEntry
    {
        [FieldOffset(0)] public double3 PlayerAup;
        [FieldOffset(24)] public float FinalLowFrequency01;
        [FieldOffset(28)] public float FinalHighFrequency01;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public uint RawSignalCount;
        [FieldOffset(40)] public uint DroppedSignalCount;
        [FieldOffset(44)] public uint BurstExecutionMicroseconds;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public float GlobalQualityWeight;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint GeneratedPulseCount;
    }

    public static class HapticSynthesisMath
    {
        public const int PulseCapacity = 64;
        public const int MockImpulseCapacity = 64;
        public const int ProfileCapacity = 32;
        public const int TelemetryCapacity = 300;
        public const int ProfileCsvScratchBytes = 4096;
        public const uint DefaultMaterialHash = 0x48415054u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveTickInterval(float globalQualityWeight)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            return math.lerp(0.016f, 0.1f, 1f - quality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ResolveProfileScanCount(uint profileCount, float globalQualityWeight)
        {
            if (profileCount == 0u)
                return 0u;

            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float detailWeight = quality * quality * (3f - (2f * quality));
            float scanFraction = math.lerp(0.125f, 1f, detailWeight);
            uint scanCount = (uint)math.max(1, (int)math.ceil((float)profileCount * scanFraction));
            return math.min(profileCount, scanCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HapticTuningDTO DefaultTuning(float globalQualityWeight)
        {
            float quality = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            HapticTuningDTO tuning = default;
            tuning.DistanceAttenuationCurve = 1f;
            tuning.GlobalRumbleMultiplier = 1f;
            tuning.MaxMotorAmplitude = 0.92f;
            tuning.MicroscopicThreshold = 0.00018f;
            tuning.GlobalQualityWeight = quality;
            tuning.TickIntervalSeconds = ResolveTickInterval(quality);
            tuning.ProfileCount = 0u;
            tuning.Flags = 0u;
            return tuning;
        }

        public static uint ValidateLayoutSizes()
        {
            uint mask = 0u;
            mask |= UnsafeUtility.SizeOf<HapticPulseSignal>() == 16 ? 0u : 1u << 0;
            mask |= UnsafeUtility.SizeOf<HapticPhysicalImpulseDTO>() == 64 ? 0u : 1u << 1;
            mask |= UnsafeUtility.SizeOf<HapticProfileDTO>() == 32 ? 0u : 1u << 2;
            mask |= UnsafeUtility.SizeOf<HapticTuningDTO>() == 32 ? 0u : 1u << 3;
            mask |= UnsafeUtility.SizeOf<HapticTelemetryEntry>() == 64 ? 0u : 1u << 4;
            return mask;
        }

        public static int WriteDefaultProfiles(NativeArray<HapticProfileDTO> profiles)
        {
            if (!profiles.IsCreated || profiles.Length <= 0)
                return 0;

            int count = math.min(profiles.Length, 5);
            profiles[0] = BuildProfile(DefaultMaterialHash, 1f, 0.65f, 1f, 0f, 1f);
            if (count > 1) profiles[1] = BuildProfile(Fnv1A("Titanium_Hull"), 1.25f, 0.35f, 1.15f, -0.2f, 1f);
            if (count > 2) profiles[2] = BuildProfile(Fnv1A("Glass_Crack"), 0.35f, 1.35f, 0.55f, 0.35f, 1f);
            if (count > 3) profiles[3] = BuildProfile(Fnv1A("Rock_Impact"), 1.1f, 0.45f, 1f, -0.05f, 1.1f);
            if (count > 4) profiles[4] = BuildProfile(Fnv1A("Laser_Cutter"), 0.2f, 1.25f, 0.4f, 0.5f, 1f);
            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HapticProfileDTO BuildProfile(uint materialHash, float lowGain, float highGain, float durationScale, float sharpnessBias, float distanceBias)
        {
            HapticProfileDTO profile = default;
            profile.MaterialHash = materialHash == 0u ? DefaultMaterialHash : materialHash;
            profile.LowGain = math.max(0f, math.isfinite(lowGain) ? lowGain : 1f);
            profile.HighGain = math.max(0f, math.isfinite(highGain) ? highGain : 0.65f);
            profile.DurationScale = math.clamp(math.isfinite(durationScale) ? durationScale : 1f, 0.05f, 4f);
            profile.SharpnessBias = math.clamp(math.isfinite(sharpnessBias) ? sharpnessBias : 0f, -1f, 1f);
            profile.DistanceBias = math.max(0.05f, math.isfinite(distanceBias) ? distanceBias : 1f);
            profile.Flags = 0u;
            return profile;
        }

        public static uint Fnv1A(string value)
        {
            if (string.IsNullOrEmpty(value))
                return DefaultMaterialHash;

            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }

            return hash == 0u ? DefaultMaterialHash : hash;
        }
    }

#if UNITY_EDITOR
    public static class HapticProfileCsvParser
    {
        public static int ParseProfiles(ReadOnlySpan<byte> csv, NativeArray<HapticProfileDTO> profiles)
        {
            if (csv.Length <= 0 || !profiles.IsCreated || profiles.Length <= 0)
                return 0;

            int position = 0;
            int count = 0;
            while (position < csv.Length && count < profiles.Length)
            {
                SkipLineBreaks(csv, ref position);
                if (position >= csv.Length)
                    break;

                int lineStart = position;
                int lineEnd = position;
                while (lineEnd < csv.Length && csv[lineEnd] != (byte)'\n' && csv[lineEnd] != (byte)'\r')
                    lineEnd++;

                position = lineEnd + 1;
                if (lineEnd <= lineStart || csv[lineStart] == (byte)'#')
                    continue;

                int cursor = lineStart;
                int nameStart = cursor;
                while (cursor < lineEnd && csv[cursor] != (byte)',')
                    cursor++;

                if (cursor <= nameStart)
                    continue;

                uint materialHash = HashBytes(csv.Slice(nameStart, cursor - nameStart));
                if (materialHash == HapticSynthesisMath.Fnv1A("material"))
                    continue;

                cursor++;
                float lowGain = ParseFloat(csv, ref cursor, lineEnd, 1f);
                float highGain = ParseFloat(csv, ref cursor, lineEnd, 0.65f);
                float durationScale = ParseFloat(csv, ref cursor, lineEnd, 1f);
                float sharpnessBias = ParseFloat(csv, ref cursor, lineEnd, 0f);
                float distanceBias = ParseFloat(csv, ref cursor, lineEnd, 1f);
                profiles[count++] = HapticSynthesisMath.BuildProfile(
                    materialHash,
                    lowGain,
                    highGain,
                    durationScale,
                    sharpnessBias,
                    distanceBias);
            }

            return count;
        }

        private static void SkipLineBreaks(ReadOnlySpan<byte> csv, ref int position)
        {
            while (position < csv.Length && (csv[position] == (byte)'\n' || csv[position] == (byte)'\r'))
                position++;
        }

        private static uint HashBytes(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte c = bytes[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash == 0u ? HapticSynthesisMath.DefaultMaterialHash : hash;
        }

        private static float ParseFloat(ReadOnlySpan<byte> bytes, ref int cursor, int lineEnd, float fallback)
        {
            while (cursor < lineEnd && bytes[cursor] == (byte)' ')
                cursor++;

            bool negative = cursor < lineEnd && bytes[cursor] == (byte)'-';
            if (negative)
                cursor++;

            double value = 0d;
            bool any = false;
            while (cursor < lineEnd)
            {
                byte c = bytes[cursor];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                value = (value * 10d) + (c - (byte)'0');
                any = true;
                cursor++;
            }

            if (cursor < lineEnd && bytes[cursor] == (byte)'.')
            {
                cursor++;
                double scale = 0.1d;
                while (cursor < lineEnd)
                {
                    byte c = bytes[cursor];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;
                    value += (c - (byte)'0') * scale;
                    scale *= 0.1d;
                    any = true;
                    cursor++;
                }
            }

            while (cursor < lineEnd && bytes[cursor] != (byte)',')
                cursor++;
            if (cursor < lineEnd && bytes[cursor] == (byte)',')
                cursor++;

            if (!any)
                return fallback;

            float result = (float)(negative ? -value : value);
            return math.isfinite(result) ? result : fallback;
        }
    }
#endif

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockHapticStormJob : IJob
    {
        [NoAlias] public NativeArray<HapticPhysicalImpulseDTO> Impulses;
        public double3 PlayerAup;
        public uint Frame;
        public uint Seed;

        public void Execute()
        {
            if (!Impulses.IsCreated || Impulses.Length <= 0)
                return;

            HapticPhysicalImpulseDTO* impulses = (HapticPhysicalImpulseDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Impulses);
            int capacity = math.min(Impulses.Length, 51);
            uint state = Seed == 0u ? 0x9E3779B9u : Seed;
            for (int i = 0; i < capacity; i++)
            {
                state = (state * 1664525u) + 1013904223u;
                float x = (((state >> 8) & 1023u) * (1f / 1023f) - 0.5f) * 16f;
                state = (state * 1664525u) + 1013904223u;
                float y = (((state >> 8) & 1023u) * (1f / 1023f) - 0.5f) * 4f;
                state = (state * 1664525u) + 1013904223u;
                float z = (((state >> 8) & 1023u) * (1f / 1023f) - 0.5f) * 16f;

                HapticPhysicalImpulseDTO impulse = default;
                impulse.ImpactAup = PlayerAup + new double3(x, y, z);
                impulse.Magnitude = i == 50 ? 8f : 0.12f + ((state & 31u) * 0.01f);
                impulse.Sharpness01 = i == 50 ? 0.18f : math.saturate(((state >> 3) & 255u) * (1f / 255f));
                impulse.MaterialHash = HapticSynthesisMath.DefaultMaterialHash;
                impulse.SourceHash = 0x53333533u;
                impulse.Frame = Frame;
                impulse.Channel = i == 50 ? (byte)2 : (byte)1;
                impulse.Flags = 0;
                UnsafeUtility.AsRef<HapticPhysicalImpulseDTO>(impulses + i) = impulse;
            }

            for (int i = capacity; i < Impulses.Length; i++)
                UnsafeUtility.AsRef<HapticPhysicalImpulseDTO>(impulses + i) = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateHapticSynthesisJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<ImpactSignal>.ReadOnly ImpactSignals;
        [ReadOnly, NoAlias] public NativeArray<HighSpeedImpactSignal>.ReadOnly HighSpeedImpactSignals;
        [ReadOnly, NoAlias] public NativeArray<CombatDamageSignal>.ReadOnly CombatDamageSignals;
        [ReadOnly, NoAlias] public NativeArray<ToolAcousticSignal>.ReadOnly ToolAcousticSignals;
        [ReadOnly, NoAlias] public NativeArray<HapticPhysicalImpulseDTO> MockImpulses;
        [ReadOnly, NoAlias] public NativeArray<HapticProfileDTO> Profiles;
        [ReadOnly, NoAlias] public NativeArray<HapticTuningDTO> Tuning;
        [NoAlias] public NativeArray<HapticPulseSignal> Pulses;
        [NoAlias] public NativeArray<HapticTelemetryEntry> TelemetryRing;

        public double3 PlayerAup;
        public uint Frame;
        public float GlobalQualityWeight;
        public int MockImpulseCount;
        public int TelemetryCursor;

        public void Execute()
        {
            if (!Pulses.IsCreated || Pulses.Length <= 0)
                return;

            HapticPulseSignal* pulsePtr = (HapticPulseSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Pulses);
            for (int i = 0; i < Pulses.Length; i++)
                UnsafeUtility.AsRef<HapticPulseSignal>(pulsePtr + i) = default;

            HapticTuningDTO tuning = ResolveTuning();
            float quality = ResolveQuality(tuning);
            float detailWeight = quality * quality * (3f - (2f * quality));
            uint activeProfileCount = HapticSynthesisMath.ResolveProfileScanCount(tuning.ProfileCount, quality);
            int pulseIndex = 0;
            uint rawCount = 0u;
            uint droppedCount = 0u;
            uint flags = 0u;

            for (int i = 0; i < ImpactSignals.Length; i++)
            {
                ImpactSignal signal = ImpactSignals[i];
                double3 aup = ToAbsoluteDouble3(in signal.PointAup);
                float magnitude = math.max(math.abs(signal.Force), math.abs(signal.Intensity));
                AddSpatialPulse(ref pulseIndex, ref rawCount, ref droppedCount, ref flags, in tuning, detailWeight, activeProfileCount, aup, magnitude * 0.04f, 0.35f, signal.MaterialHash, signal.PrimaryBodyId, HapticPulseSignal.PriorityCollision);
            }

            for (int i = 0; i < HighSpeedImpactSignals.Length; i++)
            {
                HighSpeedImpactSignal signal = HighSpeedImpactSignals[i];
                double3 aup = ToAbsoluteDouble3(in signal.PointAup);
                float energy = math.max(math.abs(signal.LostKineticEnergy), math.abs(signal.ImpactSpeed * math.max(signal.EffectiveMass, 0.1f)));
                float sharpness = math.saturate(signal.ImpactSpeed * 0.025f);
                AddSpatialPulse(ref pulseIndex, ref rawCount, ref droppedCount, ref flags, in tuning, detailWeight, activeProfileCount, aup, energy * 0.003f, sharpness, signal.MaterialHash, signal.SourceHash, HapticPulseSignal.PriorityCollision);
            }

            for (int i = 0; i < CombatDamageSignals.Length; i++)
            {
                CombatDamageSignal signal = CombatDamageSignals[i];
                float sharpness = math.saturate(math.length(signal.Direction));
                AddSpatialPulse(ref pulseIndex, ref rawCount, ref droppedCount, ref flags, in tuning, detailWeight, activeProfileCount, signal.ImpactAup, signal.Magnitude, sharpness, signal.DamageType, signal.SourceHash, HapticPulseSignal.PriorityExplosion);
            }

            int safeMockCount = math.min(math.max(MockImpulseCount, 0), MockImpulses.IsCreated ? MockImpulses.Length : 0);
            for (int i = 0; i < safeMockCount; i++)
            {
                HapticPhysicalImpulseDTO impulse = MockImpulses[i];
                AddSpatialPulse(ref pulseIndex, ref rawCount, ref droppedCount, ref flags, in tuning, detailWeight, activeProfileCount, impulse.ImpactAup, impulse.Magnitude, impulse.Sharpness01, impulse.MaterialHash, impulse.SourceHash, HapticPulseSignal.PriorityExplosion);
            }

            if (safeMockCount > 0)
                flags |= HapticSynthesisFaultFlags.MockStormActive;

            for (int i = 0; i < ToolAcousticSignals.Length; i++)
            {
                ToolAcousticSignal signal = ToolAcousticSignals[i];
                rawCount++;
                float progress = math.saturate(math.isfinite(signal.Progress01) ? signal.Progress01 : 0f);
                float intensity = math.saturate(math.isfinite(signal.Intensity01) ? signal.Intensity01 : 0f) * tuning.GlobalRumbleMultiplier;
                float heat = progress * progress * (3f - (2f * progress));
                HapticPulseSignal pulse = default;
                pulse.LowFrequencyMotor01 = math.saturate(intensity * math.lerp(0.08f, 0.22f, 1f - heat));
                pulse.HighFrequencyMotor01 = math.saturate(intensity * math.lerp(0.35f, 1f, heat));
                pulse.DurationSeconds = math.lerp(0.025f, 0.08f, heat);
                pulse.PriorityFlags = HapticPulseSignal.PriorityTool | (signal.ToolHash & 0x00FFFFFFu);
                WritePulse(ref pulseIndex, ref droppedCount, ref flags, pulse);
            }

            WriteTelemetry(in tuning, rawCount, droppedCount, flags, (uint)pulseIndex);
        }

        private HapticTuningDTO ResolveTuning()
        {
            HapticTuningDTO tuning = Tuning.IsCreated && Tuning.Length > 0
                ? Tuning[0]
                : HapticSynthesisMath.DefaultTuning(GlobalQualityWeight);
            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : tuning.GlobalQualityWeight);
            tuning.GlobalQualityWeight = quality;
            tuning.TickIntervalSeconds = HapticSynthesisMath.ResolveTickInterval(quality);
            tuning.DistanceAttenuationCurve = math.max(0.0001f, math.isfinite(tuning.DistanceAttenuationCurve) ? tuning.DistanceAttenuationCurve : 1f);
            tuning.GlobalRumbleMultiplier = math.max(0f, math.isfinite(tuning.GlobalRumbleMultiplier) ? tuning.GlobalRumbleMultiplier : 1f);
            tuning.MaxMotorAmplitude = math.saturate(math.isfinite(tuning.MaxMotorAmplitude) ? tuning.MaxMotorAmplitude : 0.92f);
            tuning.MicroscopicThreshold = math.max(0.000001f, math.isfinite(tuning.MicroscopicThreshold) ? tuning.MicroscopicThreshold : 0.00018f);
            tuning.ProfileCount = math.min(tuning.ProfileCount, (uint)(Profiles.IsCreated ? Profiles.Length : 0));
            return tuning;
        }

        private static float ResolveQuality(in HapticTuningDTO tuning)
        {
            return math.saturate(math.isfinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : 1f);
        }

        private void AddSpatialPulse(
            ref int pulseIndex,
            ref uint rawCount,
            ref uint droppedCount,
            ref uint flags,
            in HapticTuningDTO tuning,
            float detailWeight,
            uint activeProfileCount,
            double3 eventAup,
            float magnitude,
            float sharpness01,
            uint materialHash,
            uint sourceHash,
            uint priorityFlags)
        {
            rawCount++;
            if (!math.all(math.isfinite(eventAup)) || !math.all(math.isfinite(PlayerAup)) || !math.isfinite(magnitude))
            {
                flags |= HapticSynthesisFaultFlags.NanSanitized;
                return;
            }

            double3 deltaDouble = eventAup - PlayerAup;
            if (!math.all(math.isfinite(deltaDouble)))
            {
                flags |= HapticSynthesisFaultFlags.NanSanitized;
                return;
            }

            float3 delta = new float3((float)deltaDouble.x, (float)deltaDouble.y, (float)deltaDouble.z);
            float distanceSq = math.max(math.lengthsq(delta), 1f);
            HapticProfileDTO profile = ResolveProfile(materialHash, activeProfileCount);
            float rawIntensity = math.max(0f, magnitude) * tuning.GlobalRumbleMultiplier;
            float attenuated = rawIntensity / math.max(distanceSq * math.max(profile.DistanceBias * tuning.DistanceAttenuationCurve, 0.0001f), 1f);
            if (attenuated < tuning.MicroscopicThreshold)
            {
                droppedCount++;
                return;
            }

            float sharpness = math.saturate(sharpness01 + profile.SharpnessBias);
            float sharpCurve = sharpness * sharpness * (3f - (2f * sharpness));
            float lowGain = math.lerp(1f, profile.LowGain * (1f - (sharpCurve * 0.35f)), detailWeight);
            float highGain = math.lerp(math.max(0.12f, sharpness), profile.HighGain * sharpCurve, detailWeight);
            HapticPulseSignal pulse = default;
            pulse.LowFrequencyMotor01 = math.saturate(attenuated * lowGain);
            pulse.HighFrequencyMotor01 = math.saturate(attenuated * highGain);
            pulse.DurationSeconds = math.clamp((0.025f + (attenuated * 0.2f)) * profile.DurationScale, 0.015f, 0.35f);
            pulse.PriorityFlags = priorityFlags | (sourceHash & 0x00FFFFFFu);
            WritePulse(ref pulseIndex, ref droppedCount, ref flags, pulse);
        }

        private HapticProfileDTO ResolveProfile(uint materialHash, uint profileCount)
        {
            int count = math.min((int)profileCount, Profiles.IsCreated ? Profiles.Length : 0);
            for (int i = 0; i < count; i++)
            {
                HapticProfileDTO profile = Profiles[i];
                if (profile.MaterialHash == materialHash && profile.MaterialHash != 0u)
                    return profile;
            }

            return HapticSynthesisMath.BuildProfile(HapticSynthesisMath.DefaultMaterialHash, 1f, 0.65f, 1f, 0f, 1f);
        }

        private void WritePulse(ref int pulseIndex, ref uint droppedCount, ref uint flags, HapticPulseSignal pulse)
        {
            if (!math.isfinite(pulse.LowFrequencyMotor01) ||
                !math.isfinite(pulse.HighFrequencyMotor01) ||
                !math.isfinite(pulse.DurationSeconds))
            {
                flags |= HapticSynthesisFaultFlags.NanSanitized;
                return;
            }

            if (pulseIndex >= Pulses.Length)
            {
                droppedCount++;
                flags |= HapticSynthesisFaultFlags.PulseOverflow;
                return;
            }

            HapticPulseSignal* pulsePtr = (HapticPulseSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Pulses);
            UnsafeUtility.AsRef<HapticPulseSignal>(pulsePtr + pulseIndex) = pulse;
            pulseIndex++;
        }

        private void WriteTelemetry(in HapticTuningDTO tuning, uint rawCount, uint droppedCount, uint flags, uint generatedPulseCount)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int index = math.clamp(TelemetryCursor, 0, TelemetryRing.Length - 1);
            HapticTelemetryEntry entry = default;
            entry.PlayerAup = PlayerAup;
            entry.Frame = Frame;
            entry.RawSignalCount = rawCount;
            entry.DroppedSignalCount = droppedCount;
            entry.Flags = flags;
            entry.GlobalQualityWeight = tuning.GlobalQualityWeight;
            entry.GeneratedPulseCount = generatedPulseCount;
            entry.StateHash = Mix(Mix(rawCount, droppedCount), generatedPulseCount);
            HapticTelemetryEntry* telemetryPtr = (HapticTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(TelemetryRing);
            UnsafeUtility.AsRef<HapticTelemetryEntry>(telemetryPtr + index) = entry;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ToAbsoluteDouble3(in AbsoluteUniversePosition aup)
        {
            double cell = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                ((double)aup.GridX * cell) + aup.LocalX,
                ((double)aup.GridY * cell) + aup.LocalY,
                ((double)aup.GridZ * cell) + aup.LocalZ);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint a, uint b)
        {
            uint hash = 2166136261u;
            hash = (hash ^ a) * 16777619u;
            hash = (hash ^ b) * 16777619u;
            return hash == 0u ? 1u : hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CoalesceHapticPulsesJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<HapticPulseSignal> Pulses;
        [ReadOnly, NoAlias] public NativeArray<HapticTuningDTO> Tuning;
        [NoAlias] public NativeArray<HapticPulseSignal> FinalPulse;
        [NoAlias] public NativeArray<HapticTelemetryEntry> TelemetryRing;
        public int TelemetryCursor;
        public float GlobalQualityWeight;

        public void Execute()
        {
            if (!FinalPulse.IsCreated || FinalPulse.Length <= 0)
                return;

            HapticTuningDTO tuning = Tuning.IsCreated && Tuning.Length > 0
                ? Tuning[0]
                : HapticSynthesisMath.DefaultTuning(GlobalQualityWeight);
            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : tuning.GlobalQualityWeight);
            float detailWeight = quality * quality * (3f - (2f * quality));
            float maxAmplitude = math.saturate(math.isfinite(tuning.MaxMotorAmplitude) ? tuning.MaxMotorAmplitude : 0.92f);
            float lowMax = 0f;
            float highMax = 0f;
            float lowWeighted = 0f;
            float highWeighted = 0f;
            float durationMax = 0f;
            uint priorityFlags = 0u;
            uint faultFlags = 0u;
            int pulseLimit = Pulses.Length;
            int telemetryIndex = -1;
            HapticTelemetryEntry* telemetryPtr = null;

            if (TelemetryRing.IsCreated && TelemetryRing.Length > 0)
            {
                telemetryIndex = math.clamp(TelemetryCursor, 0, TelemetryRing.Length - 1);
                telemetryPtr = (HapticTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(TelemetryRing);
                uint generatedPulseCount = UnsafeUtility.AsRef<HapticTelemetryEntry>(telemetryPtr + telemetryIndex).GeneratedPulseCount;
                pulseLimit = math.min(pulseLimit, (int)math.min(generatedPulseCount, (uint)Pulses.Length));
            }

            for (int i = 0; i < pulseLimit; i++)
            {
                HapticPulseSignal pulse = Pulses[i];
                bool finite = math.isfinite(pulse.LowFrequencyMotor01) &&
                              math.isfinite(pulse.HighFrequencyMotor01) &&
                              math.isfinite(pulse.DurationSeconds);
                if (!finite)
                {
                    faultFlags |= HapticSynthesisFaultFlags.NanSanitized;
                    continue;
                }

                if (pulse.DurationSeconds <= 0f)
                    continue;

                float low = math.saturate(pulse.LowFrequencyMotor01);
                float high = math.saturate(pulse.HighFrequencyMotor01);
                lowMax = math.max(lowMax, low);
                highMax = math.max(highMax, high);
                lowWeighted = math.saturate(lowWeighted + (low * 0.25f));
                highWeighted = math.saturate(highWeighted + (high * 0.25f));
                durationMax = math.max(durationMax, pulse.DurationSeconds);
                priorityFlags |= pulse.PriorityFlags;
            }

            HapticPulseSignal final = default;
            final.LowFrequencyMotor01 = math.min(maxAmplitude, math.lerp(lowMax, math.max(lowMax, lowWeighted), detailWeight));
            final.HighFrequencyMotor01 = math.min(maxAmplitude, math.lerp(highMax, math.max(highMax, highWeighted), detailWeight));
            final.DurationSeconds = durationMax > 0f ? math.clamp(durationMax, 0.015f, 0.35f) : 0f;
            final.PriorityFlags = priorityFlags | ((faultFlags & HapticSynthesisFaultFlags.NanSanitized) != 0u ? HapticPulseSignal.FlagNanSanitized : 0u);

            HapticPulseSignal* finalPtr = (HapticPulseSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(FinalPulse);
            UnsafeUtility.AsRef<HapticPulseSignal>(finalPtr) = final;

            if (telemetryPtr != null && telemetryIndex >= 0)
            {
                ref HapticTelemetryEntry entry = ref UnsafeUtility.AsRef<HapticTelemetryEntry>(telemetryPtr + telemetryIndex);
                entry.FinalLowFrequency01 = final.LowFrequencyMotor01;
                entry.FinalHighFrequency01 = final.HighFrequencyMotor01;
                entry.Flags |= faultFlags;
                entry.StateHash = Mix(entry.StateHash, math.asuint(final.LowFrequencyMotor01) ^ math.asuint(final.HighFrequencyMotor01));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint a, uint b)
        {
            uint hash = 2166136261u;
            hash = (hash ^ a) * 16777619u;
            hash = (hash ^ b) * 16777619u;
            return hash == 0u ? 1u : hash;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct RecordHapticSynthesisTimingJob : IJob
    {
        [NoAlias] public NativeArray<HapticTelemetryEntry> TelemetryRing;
        public int TelemetryCursor;
        public uint BurstExecutionMicroseconds;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int index = math.clamp(TelemetryCursor, 0, TelemetryRing.Length - 1);
            HapticTelemetryEntry* telemetryPtr = (HapticTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(TelemetryRing);
            ref HapticTelemetryEntry entry = ref UnsafeUtility.AsRef<HapticTelemetryEntry>(telemetryPtr + index);
            entry.BurstExecutionMicroseconds = BurstExecutionMicroseconds;
            if (BurstExecutionMicroseconds > 200u)
                entry.Flags |= HapticSynthesisFaultFlags.BudgetExceeded;
            entry.StateHash ^= BurstExecutionMicroseconds * 16777619u;
        }
    }
}
