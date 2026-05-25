using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Animation.FaunaProcedural
{
    public static class ProceduralBoneBlenderConstants
    {
        public const int DefaultSkeletonCapacity = 5001;
        public const int DefaultBoneCapacity = 15168;
        public const int EmergencyMockBoneCount = 5;
        public const int TelemetryCapacity = 300;
        public const int TuningCapacity = 1;
        public const int TelemetryEntryBytes = 64;
        public const int BoneStateBytes = 80;
        public const int RigBytes = 96;
        public const int FrameInputBytes = 80;
        public const int FrameStatsBytes = 64;
        public const int MockAiSignalBytes = 64;
        public const int TuningBytes = 64;
        public const float MinDeltaTime = 0.0001f;
        public const float MaxDeltaTime = 0.2f;
        public const float MinDenominator = 0.0001f;
        public const float TwoPi = 6.283185307179586f;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_ANIM_SURGEON.bin";

        public const uint RigFlagEmergencyMock = 1u << 0;
        public const uint RigFlagVisible = 1u << 1;
        public const uint RigFlagHasJaw = 1u << 2;
        public const uint RigFlagTrauma = 1u << 3;
        public const uint InputFlagVisible = 1u << 0;
        public const uint InputFlagTraumaImpulse = 1u << 1;
        public const uint TelemetryFlagVisible = 1u << 0;
        public const uint TelemetryFlagQualityCollapse = 1u << 1;
        public const uint TelemetryFlagJawSolved = 1u << 2;
        public const uint TelemetryFlagMockSignal = 1u << 3;
        public const uint TelemetryFlagInvalid = 1u << 31;
    }

    public static class ProceduralBoneBlenderBufferIds
    {
        public const BufferID Rigs = (BufferID)71680;
        public const BufferID FrameInputs = (BufferID)71681;
        public const BufferID ParentIndices = (BufferID)71682;
        public const BufferID BindPoses = (BufferID)71683;
        public const BufferID BoneStates = (BufferID)71684;
        public const BufferID BoneMatrices = (BufferID)71685;
        public const BufferID FrameStats = (BufferID)71686;
        public const BufferID TelemetryRing = (BufferID)71687;
        public const BufferID TelemetryCursor = (BufferID)71688;
        public const BufferID Tuning = (BufferID)71689;
        public const BufferID MockAiSignals = (BufferID)71690;
    }

    [StructLayout(LayoutKind.Explicit, Size = ProceduralBoneBlenderConstants.BoneStateBytes)]
    public struct BoneStateDTO
    {
        [FieldOffset(0)] public float4x4 LocalMatrix;
        [FieldOffset(64)] public float Phase;
        [FieldOffset(68)] public uint BoneHash;
        [FieldOffset(72)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = ProceduralBoneBlenderConstants.RigBytes)]
    public struct ProceduralBoneRigDTO
    {
        [FieldOffset(0)] public uint SkeletonHash;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public int BoneStart;
        [FieldOffset(12)] public int BoneCount;
        [FieldOffset(16)] public int PrimaryBoneCount;
        [FieldOffset(20)] public int JawBoneIndex;
        [FieldOffset(24)] public int RootBoneIndex;
        [FieldOffset(28)] public int ReservedIndex;
        [FieldOffset(32)] public float BaseScale;
        [FieldOffset(36)] public float BoneLengthMeters;
        [FieldOffset(40)] public float BaseWaveSpeed;
        [FieldOffset(44)] public float VelocityWaveMultiplier;
        [FieldOffset(48)] public float BaseAmplitudeRadians;
        [FieldOffset(52)] public float PhaseOffset;
        [FieldOffset(56)] public float DampingRatio;
        [FieldOffset(60)] public float NaturalFrequencyHz;
        [FieldOffset(64)] public float TraumaSeconds;
        [FieldOffset(68)] public float WaveSpeedState;
        [FieldOffset(72)] public float WaveSpeedVelocityState;
        [FieldOffset(76)] public float AmplitudeState;
        [FieldOffset(80)] public float AmplitudeVelocityState;
        [FieldOffset(84)] public uint StableSeed;
        [FieldOffset(88)] public uint _pad0;
        [FieldOffset(92)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = ProceduralBoneBlenderConstants.FrameInputBytes)]
    public struct ProceduralBoneFrameInputDTO
    {
        [FieldOffset(0)] public float3 RootLocalPosition;
        [FieldOffset(12)] public float Visible01;
        [FieldOffset(16)] public quaternion RootRotation;
        [FieldOffset(32)] public float3 VelocityLocal;
        [FieldOffset(44)] public float GlobalQualityWeight;
        [FieldOffset(48)] public float3 JawTargetLocal;
        [FieldOffset(60)] public float JawOpen01;
        [FieldOffset(64)] public float SimulationTickDelta;
        [FieldOffset(68)] public float SimulationTime;
        [FieldOffset(72)] public float BaseScaleOverride;
        [FieldOffset(76)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = ProceduralBoneBlenderConstants.TuningBytes)]
    public struct ProceduralBoneRigTuningDTO
    {
        [FieldOffset(0)] public float SineFrequency;
        [FieldOffset(4)] public float WaveAmplitudeRadians;
        [FieldOffset(8)] public float PhaseOffset;
        [FieldOffset(12)] public float DampingHz;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float SecondaryBoneStart01;
        [FieldOffset(24)] public float JawIkWeight;
        [FieldOffset(28)] public float MockSignalWeight;
        [FieldOffset(32)] public float TraumaFrequencyHz;
        [FieldOffset(36)] public float TraumaAmplitudeRadians;
        [FieldOffset(40)] public float LowQualityUpdateHz;
        [FieldOffset(44)] public float HighQualityUpdateHz;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public int ActiveSkeletonCount;
        [FieldOffset(56)] public uint SectorHash;
        [FieldOffset(60)] public uint _pad0;

        public static ProceduralBoneRigTuningDTO Default()
        {
            ProceduralBoneRigTuningDTO value = default;
            value.SineFrequency = 1.35f;
            value.WaveAmplitudeRadians = 0.32f;
            value.PhaseOffset = 0.72f;
            value.DampingHz = 6.5f;
            value.GlobalQualityWeight = 1f;
            value.SecondaryBoneStart01 = 0.28f;
            value.JawIkWeight = 1f;
            value.MockSignalWeight = 1f;
            value.TraumaFrequencyHz = 15f;
            value.TraumaAmplitudeRadians = 0.18f;
            value.LowQualityUpdateHz = 5f;
            value.HighQualityUpdateHz = 60f;
            value.Flags = ProceduralBoneBlenderConstants.RigFlagEmergencyMock;
            value.ActiveSkeletonCount = 1;
            value.SectorHash = 0x53484E42u;
            value._pad0 = 0u;
            return value;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = ProceduralBoneBlenderConstants.MockAiSignalBytes)]
    public partial struct MockAiVelocitySignal
    {
        [FieldOffset(0)] public float3 VelocityLocal;
        [FieldOffset(12)] public float Weight01;
        [FieldOffset(16)] public float3 IkTargetLocal;
        [FieldOffset(28)] public float JawOpen01;
        [FieldOffset(32)] public uint EntityHash;
        [FieldOffset(36)] public uint SectorHash;
        [FieldOffset(40)] public uint SimulationFrame;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public float NoisePhase;
        [FieldOffset(52)] public float SpeedHint;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = ProceduralBoneBlenderConstants.FrameStatsBytes)]
    public struct ProceduralBoneFrameStatsDTO
    {
        [FieldOffset(0)] public int ActiveSkeletons;
        [FieldOffset(4)] public int MatricesComputed;
        [FieldOffset(8)] public int InvalidMathCount;
        [FieldOffset(12)] public int CulledSkeletons;
        [FieldOffset(16)] public int MaxMatrixIndexPlusOne;
        [FieldOffset(20)] public float Quality;
        [FieldOffset(24)] public uint StateHash;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float MaxWaveSpeed;
        [FieldOffset(36)] public float AverageActiveBones;
        [FieldOffset(40)] public float3 LastRootLocal;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = ProceduralBoneBlenderConstants.TelemetryEntryBytes)]
    public struct ProceduralBoneTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public int ActiveSkeletons;
        [FieldOffset(8)] public int MatricesComputed;
        [FieldOffset(12)] public int MatrixUploadCount;
        [FieldOffset(16)] public float KinematicComputeTimeMs;
        [FieldOffset(20)] public uint StateHash;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public float MaxWaveSpeed;
        [FieldOffset(36)] public float AverageActiveBones;
        [FieldOffset(40)] public int InvalidMathCount;
        [FieldOffset(44)] public int CulledSkeletons;
        [FieldOffset(48)] public float3 LastRootLocal;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ProceduralBoneCounter64
    {
        [FieldOffset(0)] public int Value;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public ulong _pad0;
        [FieldOffset(16)] public ulong _pad1;
        [FieldOffset(24)] public ulong _pad2;
        [FieldOffset(32)] public ulong _pad3;
        [FieldOffset(40)] public ulong _pad4;
        [FieldOffset(48)] public ulong _pad5;
        [FieldOffset(56)] public ulong _pad6;
    }

    public static class ProceduralBoneBlenderLayout
    {
        public static bool Validate()
        {
            return UnsafeUtility.SizeOf<BoneStateDTO>() == ProceduralBoneBlenderConstants.BoneStateBytes &&
                   UnsafeUtility.SizeOf<ProceduralBoneRigDTO>() == ProceduralBoneBlenderConstants.RigBytes &&
                   UnsafeUtility.SizeOf<ProceduralBoneFrameInputDTO>() == ProceduralBoneBlenderConstants.FrameInputBytes &&
                   UnsafeUtility.SizeOf<ProceduralBoneRigTuningDTO>() == ProceduralBoneBlenderConstants.TuningBytes &&
                   UnsafeUtility.SizeOf<MockAiVelocitySignal>() == ProceduralBoneBlenderConstants.MockAiSignalBytes &&
                   UnsafeUtility.SizeOf<ProceduralBoneFrameStatsDTO>() == ProceduralBoneBlenderConstants.FrameStatsBytes &&
                   UnsafeUtility.SizeOf<ProceduralBoneTelemetryEntry>() == ProceduralBoneBlenderConstants.TelemetryEntryBytes &&
                   UnsafeUtility.SizeOf<ProceduralBoneCounter64>() == 64;
        }
    }

#if UNITY_EDITOR
    public static class ProceduralBoneProfileCsvParser
    {
        private const uint HashSineFrequency = 0x9BFCEDA1u;
        private const uint HashWaveAmplitude = 0xD22FEB76u;
        private const uint HashPhaseOffset = 0x3A5AF3EAu;
        private const uint HashDamping = 0xBB61B895u;
        private const uint HashGlobalQualityWeight = 0xB00FB719u;
        private const uint HashSecondaryBoneStart = 0xE63A8D4Bu;
        private const uint HashJawIkWeight = 0xF958874Fu;
        private const uint HashMockSignalWeight = 0xE94BD2C9u;
        private const uint HashTraumaFrequency = 0x558C0F66u;
        private const uint HashTraumaAmplitude = 0x13F69335u;
        private const uint HashLowUpdateHz = 0x13CCFD3Au;
        private const uint HashHighUpdateHz = 0x9F698D16u;
        private const uint HashBoneLength = 0x8356A2ECu;
        private const uint HashBaseWaveSpeed = 0x1700C3ACu;
        private const uint HashVelocityMultiplier = 0xF51E488Cu;
        private const uint HashBaseScale = 0xED20BA31u;
        private const uint HashPrimaryBoneCount = 0x0742F8C8u;
        private const uint HashActiveSkeletons = 0xC053E93Cu;

        public static bool TryApply(ReadOnlySpan<char> csv, ref ProceduralBoneRigTuningDTO tuning)
        {
            ProceduralBoneRigDTO ignoredRig = default;
            return TryApply(csv, ref tuning, ref ignoredRig);
        }

        public static bool TryApply(ReadOnlySpan<char> csv, ref ProceduralBoneRigTuningDTO tuning, ref ProceduralBoneRigDTO rig)
        {
            bool any = false;
            int index = 0;
            while (index < csv.Length)
            {
                ReadOnlySpan<char> line = ReadLine(csv, ref index);
                Trim(ref line);
                if (line.Length == 0 || line[0] == '#')
                    continue;

                int comma = IndexOfComma(line);
                if (comma <= 0 || comma >= line.Length - 1)
                    continue;

                ReadOnlySpan<char> key = line.Slice(0, comma);
                ReadOnlySpan<char> valueSpan = line.Slice(comma + 1);
                Trim(ref key);
                Trim(ref valueSpan);
                if (!TryParseFloat(valueSpan, out float value))
                    continue;

                any |= ApplyValue(key, value, ref tuning, ref rig);
            }

            tuning = ProceduralBoneSanitizer.SanitizeTuning(tuning);
            rig.BaseScale = ProceduralBoneSanitizer.SanitizePositive(rig.BaseScale, 1f);
            rig.BoneLengthMeters = ProceduralBoneSanitizer.SanitizePositive(rig.BoneLengthMeters, 1.25f);
            rig.BaseWaveSpeed = ProceduralBoneSanitizer.SanitizePositive(rig.BaseWaveSpeed, tuning.SineFrequency);
            rig.VelocityWaveMultiplier = ProceduralBoneSanitizer.SanitizePositive(rig.VelocityWaveMultiplier, 0.22f);
            rig.PrimaryBoneCount = math.max(1, rig.PrimaryBoneCount);
            return any;
        }

        private static bool ApplyValue(ReadOnlySpan<char> key, float value, ref ProceduralBoneRigTuningDTO tuning, ref ProceduralBoneRigDTO rig)
        {
            switch (HashAscii(key))
            {
                case HashSineFrequency:
                    tuning.SineFrequency = value;
                    return true;
                case HashWaveAmplitude:
                    tuning.WaveAmplitudeRadians = value;
                    return true;
                case HashPhaseOffset:
                    tuning.PhaseOffset = value;
                    return true;
                case HashDamping:
                    tuning.DampingHz = value;
                    return true;
                case HashGlobalQualityWeight:
                    tuning.GlobalQualityWeight = value;
                    return true;
                case HashSecondaryBoneStart:
                    tuning.SecondaryBoneStart01 = value;
                    return true;
                case HashJawIkWeight:
                    tuning.JawIkWeight = value;
                    return true;
                case HashMockSignalWeight:
                    tuning.MockSignalWeight = value;
                    return true;
                case HashTraumaFrequency:
                    tuning.TraumaFrequencyHz = value;
                    return true;
                case HashTraumaAmplitude:
                    tuning.TraumaAmplitudeRadians = value;
                    return true;
                case HashLowUpdateHz:
                    tuning.LowQualityUpdateHz = value;
                    return true;
                case HashHighUpdateHz:
                    tuning.HighQualityUpdateHz = value;
                    return true;
                case HashBoneLength:
                    rig.BoneLengthMeters = value;
                    return true;
                case HashBaseWaveSpeed:
                    rig.BaseWaveSpeed = value;
                    return true;
                case HashVelocityMultiplier:
                    rig.VelocityWaveMultiplier = value;
                    return true;
                case HashBaseScale:
                    rig.BaseScale = value;
                    return true;
                case HashPrimaryBoneCount:
                    rig.PrimaryBoneCount = (int)math.round(value);
                    return true;
                case HashActiveSkeletons:
                    tuning.ActiveSkeletonCount = (int)math.round(value);
                    return true;
            }
            return false;
        }

        private static ReadOnlySpan<char> ReadLine(ReadOnlySpan<char> csv, ref int index)
        {
            int start = index;
            while (index < csv.Length && csv[index] != '\n' && csv[index] != '\r')
                index++;

            ReadOnlySpan<char> line = csv.Slice(start, index - start);
            while (index < csv.Length && (csv[index] == '\n' || csv[index] == '\r'))
                index++;

            return line;
        }

        private static int IndexOfComma(ReadOnlySpan<char> value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == ',')
                    return i;
            }

            return -1;
        }

        private static void Trim(ref ReadOnlySpan<char> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start < value.Length && IsWhitespace(value[start]))
                start++;
            while (end >= start && IsWhitespace(value[end]))
                end--;

            value = start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<char>.Empty;
        }

        private static bool IsWhitespace(char value)
        {
            return value == ' ' || value == '\t' || value == '\r' || value == '\n';
        }

        private static uint HashAscii(ReadOnlySpan<char> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash;
        }

        private static bool TryParseFloat(ReadOnlySpan<char> value, out float result)
        {
            result = 0f;
            if (value.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (value[index] == '+' || value[index] == '-')
            {
                sign = value[index] == '-' ? -1f : 1f;
                index++;
            }

            double whole = 0.0;
            bool any = false;
            while (index < value.Length && value[index] >= '0' && value[index] <= '9')
            {
                any = true;
                whole = whole * 10.0 + (value[index] - '0');
                index++;
            }

            double fraction = 0.0;
            double scale = 1.0;
            if (index < value.Length && value[index] == '.')
            {
                index++;
                while (index < value.Length && value[index] >= '0' && value[index] <= '9')
                {
                    any = true;
                    fraction = fraction * 10.0 + (value[index] - '0');
                    scale *= 10.0;
                    index++;
                }
            }

            int exponent = 0;
            int exponentSign = 1;
            if (index < value.Length && (value[index] == 'e' || value[index] == 'E'))
            {
                index++;
                if (index < value.Length && (value[index] == '+' || value[index] == '-'))
                {
                    exponentSign = value[index] == '-' ? -1 : 1;
                    index++;
                }

                bool anyExponent = false;
                while (index < value.Length && value[index] >= '0' && value[index] <= '9')
                {
                    anyExponent = true;
                    exponent = math.min(38, exponent * 10 + (value[index] - '0'));
                    index++;
                }

                if (!anyExponent)
                    return false;
            }

            if (!any || index != value.Length)
                return false;

            double parsed = sign * (whole + fraction / scale);
            if (exponent != 0)
                parsed = ScaleByFloatPow10(parsed, exponentSign * exponent);

            result = (float)parsed;
            return math.isfinite(result);
        }

        private static double ScaleByFloatPow10(double value, int exponent)
        {
            if (value == 0d || exponent == 0)
                return value;
            if (exponent > 38)
                return value > 0d ? double.PositiveInfinity : double.NegativeInfinity;
            if (exponent < -46)
                return 0d;

            int count = exponent < 0 ? -exponent : exponent;
            double scale = 1d;
            for (int i = 0; i < count; i++)
                scale *= 10d;

            return exponent < 0 ? value / scale : value * scale;
        }
    }

    public static class ProceduralBoneSanitizer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ProceduralBoneRigTuningDTO SanitizeTuning(ProceduralBoneRigTuningDTO value)
        {
            ProceduralBoneRigTuningDTO fallback = ProceduralBoneRigTuningDTO.Default();
            value.SineFrequency = SanitizePositive(value.SineFrequency, fallback.SineFrequency);
            value.WaveAmplitudeRadians = SanitizePositive(value.WaveAmplitudeRadians, fallback.WaveAmplitudeRadians);
            value.PhaseOffset = SanitizePositive(value.PhaseOffset, fallback.PhaseOffset);
            value.DampingHz = SanitizePositive(value.DampingHz, fallback.DampingHz);
            value.GlobalQualityWeight = Sanitize01(value.GlobalQualityWeight, fallback.GlobalQualityWeight);
            value.SecondaryBoneStart01 = math.clamp(Sanitize01(value.SecondaryBoneStart01, fallback.SecondaryBoneStart01), 0.05f, 0.95f);
            value.JawIkWeight = Sanitize01(value.JawIkWeight, fallback.JawIkWeight);
            value.MockSignalWeight = Sanitize01(value.MockSignalWeight, fallback.MockSignalWeight);
            value.TraumaFrequencyHz = SanitizePositive(value.TraumaFrequencyHz, fallback.TraumaFrequencyHz);
            value.TraumaAmplitudeRadians = SanitizePositive(value.TraumaAmplitudeRadians, fallback.TraumaAmplitudeRadians);
            value.LowQualityUpdateHz = math.clamp(SanitizePositive(value.LowQualityUpdateHz, fallback.LowQualityUpdateHz), 1f, 30f);
            value.HighQualityUpdateHz = math.clamp(SanitizePositive(value.HighQualityUpdateHz, fallback.HighQualityUpdateHz), value.LowQualityUpdateHz, 120f);
            value.ActiveSkeletonCount = math.max(0, value.ActiveSkeletonCount);
            value.SectorHash = value.SectorHash != 0u ? value.SectorHash : fallback.SectorHash;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sanitize01(float value, float fallback)
        {
            return math.saturate(math.select(fallback, value, math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }
    }

    public static class ProceduralBoneBlackBox
    {
        public static bool TryDumpTelemetry(
            string projectRoot,
            NativeArray<ProceduralBoneTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryCursor)
        {
            if (!ProceduralBoneBlenderLayout.Validate() ||
                !telemetryRing.IsCreated ||
                telemetryRing.Length < ProceduralBoneBlenderConstants.TelemetryCapacity ||
                !telemetryCursor.IsCreated ||
                telemetryCursor.Length <= 0)
            {
                return false;
            }

            try
            {
                string root = string.IsNullOrEmpty(projectRoot) ? "." : projectRoot;
                string path = Path.Combine(root, ProceduralBoneBlenderConstants.DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    int cursor = telemetryCursor[0];
                    writer.Write(0x414E494D53484E42UL);
                    writer.Write(1u);
                    writer.Write(ProceduralBoneBlenderConstants.TelemetryEntryBytes);
                    writer.Write(ProceduralBoneBlenderConstants.TelemetryCapacity);
                    writer.Write(cursor);
                    int start = cursor >= ProceduralBoneBlenderConstants.TelemetryCapacity
                        ? PositiveModulo(cursor - ProceduralBoneBlenderConstants.TelemetryCapacity, telemetryRing.Length)
                        : 0;

                    for (int i = 0; i < ProceduralBoneBlenderConstants.TelemetryCapacity; i++)
                    {
                        int sourceIndex = PositiveModulo(start + i, telemetryRing.Length);
                        WriteEntry(writer, telemetryRing[sourceIndex]);
                    }
                }

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
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        private static int PositiveModulo(int value, int length)
        {
            int safeLength = Math.Max(1, length);
            int result = value % safeLength;
            return result < 0 ? result + safeLength : result;
        }

        private static void WriteEntry(BinaryWriter writer, ProceduralBoneTelemetryEntry entry)
        {
            writer.Write(entry.Frame);
            writer.Write(entry.ActiveSkeletons);
            writer.Write(entry.MatricesComputed);
            writer.Write(entry.MatrixUploadCount);
            writer.Write(entry.KinematicComputeTimeMs);
            writer.Write(entry.StateHash);
            writer.Write(entry.Flags);
            writer.Write(entry.GlobalQualityWeight);
            writer.Write(entry.MaxWaveSpeed);
            writer.Write(entry.AverageActiveBones);
            writer.Write(entry.InvalidMathCount);
            writer.Write(entry.CulledSkeletons);
            WriteFloat3(writer, entry.LastRootLocal);
            writer.Write(entry._pad0);
        }

        private static void WriteFloat3(BinaryWriter writer, float3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }
    }
#endif
}
