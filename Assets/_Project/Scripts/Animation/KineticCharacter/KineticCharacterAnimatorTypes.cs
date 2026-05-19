using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Animation.KineticCharacter
{
    public static class KineticCharacterAnimatorConstants
    {
        public const int CharacterCapacity = 1;
        public const int DefaultBoneCapacity = 32;
        public const int EmergencyMockBoneCount = 18;
        public const int IkTargetCount = 4;
        public const int TelemetryCapacity = 300;
        public const int TuningCapacity = 1;
        public const int ProceduralBoneBytes = 64;
        public const int ProceduralIkTargetBytes = 32;
        public const int RigBytes = 192;
        public const int FrameInputBytes = 256;
        public const int TuningBytes = 128;
        public const int FrameStatsBytes = 64;
        public const int TelemetryEntryBytes = 64;
        public const float MinDeltaTime = 0.0001f;
        public const float MaxDeltaTime = 0.2f;
        public const float TwoPi = 6.283185307179586f;
        public const float Pi = 3.141592653589793f;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_136.bin";

        public const uint RigFlagEmergencyMock = 1u << 0;
        public const uint RigFlagVisible = 1u << 1;
        public const uint RigFlagHasToolSocket = 1u << 2;

        public const uint InputFlagVisible = 1u << 0;
        public const uint InputFlagMock = 1u << 1;
        public const uint InputFlagToolActive = 1u << 2;
        public const uint InputFlagDamageImpulse = 1u << 3;
        public const uint InputFlagSurfaceSwim = 1u << 4;

        public const uint IkTargetFlagLeftHand = 1u << 0;
        public const uint IkTargetFlagRightHand = 1u << 1;
        public const uint IkTargetFlagLeftFoot = 1u << 2;
        public const uint IkTargetFlagRightFoot = 1u << 3;
        public const uint IkTargetFlagSdfBrace = 1u << 4;
        public const uint IkTargetFlagPlayerKinematics = 1u << 5;

        public const uint TelemetryFlagVisible = 1u << 0;
        public const uint TelemetryFlagMock = 1u << 1;
        public const uint TelemetryFlagSdfBrace = 1u << 2;
        public const uint TelemetryFlagPlayerKinematicsTargets = 1u << 3;
        public const uint TelemetryFlagToolAligned = 1u << 4;
        public const uint TelemetryFlagDamageFlinch = 1u << 5;
        public const uint TelemetryFlagQualityCollapsed = 1u << 6;
        public const uint TelemetryFlagInvalid = 1u << 31;
    }

    public static class KineticCharacterAnimatorBufferIds
    {
        public const BufferID Rigs = (BufferID)71360;
        public const BufferID FrameInputs = (BufferID)71361;
        public const BufferID ParentIndices = (BufferID)71362;
        public const BufferID BindPoses = (BufferID)71363;
        public const BufferID BoneOutputs = (BufferID)71364;
        public const BufferID BoneMatrices = (BufferID)71365;
        public const BufferID IkTargets = (BufferID)71366;
        public const BufferID FrameStats = (BufferID)71367;
        public const BufferID TelemetryRing = (BufferID)71368;
        public const BufferID TelemetryCursor = (BufferID)71369;
        public const BufferID Tuning = (BufferID)71370;
    }

    [StructLayout(LayoutKind.Explicit, Size = KineticCharacterAnimatorConstants.ProceduralBoneBytes)]
    public struct ProceduralBoneDTO
    {
        [FieldOffset(0)] public float4x4 LocalToWorld;
    }

    [StructLayout(LayoutKind.Explicit, Size = KineticCharacterAnimatorConstants.ProceduralIkTargetBytes)]
    public struct ProceduralIKTargetDTO
    {
        [FieldOffset(0)] public float3 LocalPosition;
        [FieldOffset(12)] public float Weight01;
        [FieldOffset(16)] public float3 PoleOrNormal;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = KineticCharacterAnimatorConstants.RigBytes)]
    public struct KineticCharacterRigDTO
    {
        [FieldOffset(0)] public uint SkeletonHash;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public int BoneStart;
        [FieldOffset(12)] public int BoneCount;
        [FieldOffset(16)] public int RootIndex;
        [FieldOffset(20)] public int SpineIndex;
        [FieldOffset(24)] public int ChestIndex;
        [FieldOffset(28)] public int NeckIndex;
        [FieldOffset(32)] public int HeadIndex;
        [FieldOffset(36)] public int LeftShoulderIndex;
        [FieldOffset(40)] public int LeftElbowIndex;
        [FieldOffset(44)] public int LeftHandIndex;
        [FieldOffset(48)] public int RightShoulderIndex;
        [FieldOffset(52)] public int RightElbowIndex;
        [FieldOffset(56)] public int RightHandIndex;
        [FieldOffset(60)] public int LeftHipIndex;
        [FieldOffset(64)] public int LeftKneeIndex;
        [FieldOffset(68)] public int LeftFootIndex;
        [FieldOffset(72)] public int RightHipIndex;
        [FieldOffset(76)] public int RightKneeIndex;
        [FieldOffset(80)] public int RightFootIndex;
        [FieldOffset(84)] public int ToolSocketIndex;
        [FieldOffset(88)] public float ShoulderWidth;
        [FieldOffset(92)] public float HipWidth;
        [FieldOffset(96)] public float ArmUpperLength;
        [FieldOffset(100)] public float ArmLowerLength;
        [FieldOffset(104)] public float LegUpperLength;
        [FieldOffset(108)] public float LegLowerLength;
        [FieldOffset(112)] public float SpineLength;
        [FieldOffset(116)] public float NeckLength;
        [FieldOffset(120)] public float BreathAmplitudeMeters;
        [FieldOffset(124)] public float LocomotionAmplitudeMeters;
        [FieldOffset(128)] public float DamageDecayHz;
        [FieldOffset(132)] public float Phase;
        [FieldOffset(136)] public float PhaseVelocity;
        [FieldOffset(140)] public float DamageSeconds;
        [FieldOffset(144)] public uint StableSeed;
        [FieldOffset(148)] public int ActiveBoneCount;
        [FieldOffset(152)] public int MaxIkIterations;
        [FieldOffset(156)] public uint RuntimeFlags;
        [FieldOffset(160)] public float ReservedFloat0;
        [FieldOffset(164)] public float ReservedFloat1;
        [FieldOffset(168)] public float ReservedFloat2;
        [FieldOffset(172)] public float ReservedFloat3;
        [FieldOffset(176)] public ulong _pad0;
        [FieldOffset(184)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = KineticCharacterAnimatorConstants.FrameInputBytes)]
    public struct KineticCharacterFrameInputDTO
    {
        [FieldOffset(0)] public long RootSectorX;
        [FieldOffset(8)] public long RootSectorY;
        [FieldOffset(16)] public long RootSectorZ;
        [FieldOffset(24)] public float3 RootLocalPosition;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public quaternion RootRotation;
        [FieldOffset(56)] public float3 VelocityLocal;
        [FieldOffset(68)] public float Visible01;
        [FieldOffset(72)] public long CameraSectorX;
        [FieldOffset(80)] public long CameraSectorY;
        [FieldOffset(88)] public long CameraSectorZ;
        [FieldOffset(96)] public float3 CameraLocalPosition;
        [FieldOffset(108)] public float StressLevel01;
        [FieldOffset(112)] public float3 CameraForwardLocal;
        [FieldOffset(124)] public float OxygenLevel01;
        [FieldOffset(128)] public float3 DamageImpulseLocal;
        [FieldOffset(140)] public float DamageImpulse01;
        [FieldOffset(144)] public float4x4 ToolPoseMatrix;
        [FieldOffset(208)] public float SimulationTickDelta;
        [FieldOffset(212)] public float SimulationTime;
        [FieldOffset(216)] public float SwimWaveForward;
        [FieldOffset(220)] public float SwimWaveLateral;
        [FieldOffset(224)] public float SwimCrestReach;
        [FieldOffset(228)] public float SwimDescentTuck;
        [FieldOffset(232)] public float SwimLeanWeight;
        [FieldOffset(236)] public float ImmersionDepth;
        [FieldOffset(240)] public float BreathingPhase;
        [FieldOffset(244)] public float ActiveToolWeight01;
        [FieldOffset(248)] public uint Frame;
        [FieldOffset(252)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = KineticCharacterAnimatorConstants.TuningBytes)]
    public struct KineticCharacterTuningDTO
    {
        [FieldOffset(0)] public float LocomotionFrequencyHz;
        [FieldOffset(4)] public float LocomotionAmplitudeMeters;
        [FieldOffset(8)] public float BreathingAmplitudeMeters;
        [FieldOffset(12)] public float BreathingFrequencyHz;
        [FieldOffset(16)] public float IkToleranceMeters;
        [FieldOffset(20)] public float GlobalQualityWeight;
        [FieldOffset(24)] public int MinimumIkIterations;
        [FieldOffset(28)] public int UltraIkIterations;
        [FieldOffset(32)] public float ArmReachScale;
        [FieldOffset(36)] public float LegReachScale;
        [FieldOffset(40)] public float WallBraceDistanceMeters;
        [FieldOffset(44)] public float WallBraceWeightScale;
        [FieldOffset(48)] public float ToolAlignmentWeight;
        [FieldOffset(52)] public float DamageFlinchRadians;
        [FieldOffset(56)] public float DamageFlinchSeconds;
        [FieldOffset(60)] public float SecondaryMotionStart01;
        [FieldOffset(64)] public uint Flags;
        [FieldOffset(68)] public int ActiveCharacterCount;
        [FieldOffset(72)] public float LowTierCadenceHz;
        [FieldOffset(76)] public float UltraTierCadenceHz;
        [FieldOffset(80)] public float SpineLeanRadians;
        [FieldOffset(84)] public float SwimBobMeters;
        [FieldOffset(88)] public float ToolHandSuppression01;
        [FieldOffset(92)] public float FootPlantWeight01;
        [FieldOffset(96)] public float _padFloat0;
        [FieldOffset(100)] public float _padFloat1;
        [FieldOffset(104)] public ulong _pad0;
        [FieldOffset(112)] public ulong _pad1;
        [FieldOffset(120)] public ulong _pad2;

        public static KineticCharacterTuningDTO Default()
        {
            KineticCharacterTuningDTO value = default;
            value.LocomotionFrequencyHz = 1.2f;
            value.LocomotionAmplitudeMeters = 0.11f;
            value.BreathingAmplitudeMeters = 0.018f;
            value.BreathingFrequencyHz = 0.22f;
            value.IkToleranceMeters = 0.008f;
            value.GlobalQualityWeight = 1f;
            value.MinimumIkIterations = 1;
            value.UltraIkIterations = 6;
            value.ArmReachScale = 1f;
            value.LegReachScale = 1f;
            value.WallBraceDistanceMeters = 0.72f;
            value.WallBraceWeightScale = 1.35f;
            value.ToolAlignmentWeight = 1f;
            value.DamageFlinchRadians = 0.19f;
            value.DamageFlinchSeconds = 0.34f;
            value.SecondaryMotionStart01 = 0.35f;
            value.Flags = KineticCharacterAnimatorConstants.RigFlagEmergencyMock;
            value.ActiveCharacterCount = 1;
            value.LowTierCadenceHz = 24f;
            value.UltraTierCadenceHz = 90f;
            value.SpineLeanRadians = 0.22f;
            value.SwimBobMeters = 0.045f;
            value.ToolHandSuppression01 = 0.55f;
            value.FootPlantWeight01 = 0.3f;
            return value;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = KineticCharacterAnimatorConstants.FrameStatsBytes)]
    public struct KineticCharacterFrameStatsDTO
    {
        [FieldOffset(0)] public int ActiveCharacters;
        [FieldOffset(4)] public int MatricesComputed;
        [FieldOffset(8)] public int InvalidMathCount;
        [FieldOffset(12)] public int MaxMatrixIndexPlusOne;
        [FieldOffset(16)] public float AverageIkIterations;
        [FieldOffset(20)] public float Quality;
        [FieldOffset(24)] public uint StateHash;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float3 LastRootLocal;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] public float CpuEstimateMicroseconds;
        [FieldOffset(52)] public int ActiveIkTargets;
        [FieldOffset(56)] public int BoneUploadCount;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = KineticCharacterAnimatorConstants.TelemetryEntryBytes)]
    public struct KineticAnimationTelemetryEntry
    {
        [FieldOffset(0)] public long RootSectorX;
        [FieldOffset(8)] public long RootSectorY;
        [FieldOffset(16)] public long RootSectorZ;
        [FieldOffset(24)] public float3 RootLocal;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public int BonesEvaluated;
        [FieldOffset(44)] public float AverageIkIterations;
        [FieldOffset(48)] public float CpuTimeMicroseconds;
        [FieldOffset(52)] public uint StateHash;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public float GlobalQualityWeight;
    }

    public static class KineticCharacterAnimatorLayout
    {
        public static bool Validate()
        {
            return UnsafeUtility.SizeOf<ProceduralBoneDTO>() == KineticCharacterAnimatorConstants.ProceduralBoneBytes &&
                   UnsafeUtility.SizeOf<ProceduralIKTargetDTO>() == KineticCharacterAnimatorConstants.ProceduralIkTargetBytes &&
                   UnsafeUtility.SizeOf<KineticCharacterRigDTO>() == KineticCharacterAnimatorConstants.RigBytes &&
                   UnsafeUtility.SizeOf<KineticCharacterFrameInputDTO>() == KineticCharacterAnimatorConstants.FrameInputBytes &&
                   UnsafeUtility.SizeOf<KineticCharacterTuningDTO>() == KineticCharacterAnimatorConstants.TuningBytes &&
                   UnsafeUtility.SizeOf<KineticCharacterFrameStatsDTO>() == KineticCharacterAnimatorConstants.FrameStatsBytes &&
                   UnsafeUtility.SizeOf<KineticAnimationTelemetryEntry>() == KineticCharacterAnimatorConstants.TelemetryEntryBytes;
        }
    }

    public static class KineticCharacterSanitizer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static KineticCharacterTuningDTO SanitizeTuning(KineticCharacterTuningDTO tuning)
        {
            tuning.LocomotionFrequencyHz = PositiveFinite(tuning.LocomotionFrequencyHz, 1.2f);
            tuning.LocomotionAmplitudeMeters = NonNegativeFinite(tuning.LocomotionAmplitudeMeters, 0.11f);
            tuning.BreathingAmplitudeMeters = NonNegativeFinite(tuning.BreathingAmplitudeMeters, 0.018f);
            tuning.BreathingFrequencyHz = PositiveFinite(tuning.BreathingFrequencyHz, 0.22f);
            tuning.IkToleranceMeters = math.clamp(PositiveFinite(tuning.IkToleranceMeters, 0.008f), 0.0005f, 0.08f);
            tuning.GlobalQualityWeight = UnitFinite(tuning.GlobalQualityWeight, 1f);
            tuning.MinimumIkIterations = math.clamp(tuning.MinimumIkIterations <= 0 ? 1 : tuning.MinimumIkIterations, 1, 6);
            tuning.UltraIkIterations = math.clamp(tuning.UltraIkIterations <= 0 ? 6 : tuning.UltraIkIterations, tuning.MinimumIkIterations, 8);
            tuning.ArmReachScale = math.clamp(PositiveFinite(tuning.ArmReachScale, 1f), 0.5f, 1.5f);
            tuning.LegReachScale = math.clamp(PositiveFinite(tuning.LegReachScale, 1f), 0.5f, 1.5f);
            tuning.WallBraceDistanceMeters = math.clamp(PositiveFinite(tuning.WallBraceDistanceMeters, 0.72f), 0.05f, 2f);
            tuning.WallBraceWeightScale = math.clamp(PositiveFinite(tuning.WallBraceWeightScale, 1.35f), 0.05f, 4f);
            tuning.ToolAlignmentWeight = UnitFinite(tuning.ToolAlignmentWeight, 1f);
            tuning.DamageFlinchRadians = math.clamp(NonNegativeFinite(tuning.DamageFlinchRadians, 0.19f), 0f, 0.8f);
            tuning.DamageFlinchSeconds = math.clamp(PositiveFinite(tuning.DamageFlinchSeconds, 0.34f), 0.01f, 2f);
            tuning.SecondaryMotionStart01 = UnitFinite(tuning.SecondaryMotionStart01, 0.35f);
            tuning.ActiveCharacterCount = math.clamp(tuning.ActiveCharacterCount <= 0 ? 1 : tuning.ActiveCharacterCount, 0, KineticCharacterAnimatorConstants.CharacterCapacity);
            tuning.LowTierCadenceHz = math.clamp(PositiveFinite(tuning.LowTierCadenceHz, 24f), 1f, 120f);
            tuning.UltraTierCadenceHz = math.clamp(PositiveFinite(tuning.UltraTierCadenceHz, 90f), tuning.LowTierCadenceHz, 144f);
            tuning.SpineLeanRadians = math.clamp(NonNegativeFinite(tuning.SpineLeanRadians, 0.22f), 0f, 0.8f);
            tuning.SwimBobMeters = math.clamp(NonNegativeFinite(tuning.SwimBobMeters, 0.045f), 0f, 0.2f);
            tuning.ToolHandSuppression01 = UnitFinite(tuning.ToolHandSuppression01, 0.55f);
            tuning.FootPlantWeight01 = UnitFinite(tuning.FootPlantWeight01, 0.3f);
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float PositiveFinite(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float NonNegativeFinite(float value, float fallback)
        {
            return math.isfinite(value) && value >= 0f ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float UnitFinite(float value, float fallback)
        {
            return math.saturate(math.select(fallback, value, math.isfinite(value)));
        }
    }

    public static class KineticCharacterMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Float3(float x, float y, float z)
        {
            return new float3(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothRange01(float value, float min, float max)
        {
            return Smooth01((value - min) * math.rcp(math.max(0.0001f, max - min)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float TriangleWaveSigned(float phase01)
        {
            float x = math.frac(phase01);
            return 1f - math.abs(x * 4f - 2f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastSin(float phaseRadians)
        {
            float x = phaseRadians - math.floor((phaseRadians + KineticCharacterAnimatorConstants.Pi) * (1f / KineticCharacterAnimatorConstants.TwoPi)) * KineticCharacterAnimatorConstants.TwoPi;
            float x2 = x * x;
            return x * (1f - x2 * 0.16666667f + x2 * x2 * 0.008333331f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            return math.isfinite(lenSq) && lenSq > 0.000001f ? value * math.rsqrt(math.max(lenSq, 0.000001f)) : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SanitizeFinite(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion SanitizeRotation(quaternion rotation)
        {
            float lenSq = math.lengthsq(rotation.value);
            return math.isfinite(lenSq) && lenSq > 0.000001f
                ? new quaternion(rotation.value * math.rsqrt(lenSq))
                : quaternion.identity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(float4x4 matrix)
        {
            return math.all(math.isfinite(matrix.c0)) &&
                   math.all(math.isfinite(matrix.c1)) &&
                   math.all(math.isfinite(matrix.c2)) &&
                   math.all(math.isfinite(matrix.c3));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4x4 BoneMatrix(float3 position, float3 direction, float3 up)
        {
            float3 forward = NormalizeSafe(direction, Float3(0f, 0f, 1f));
            float3 safeUp = NormalizeSafe(up, Float3(0f, 1f, 0f));
            quaternion rotation = quaternion.LookRotationSafe(forward, safeUp);
            return float4x4.TRS(position, rotation, Float3(1f, 1f, 1f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash(uint value, uint data)
        {
            value ^= data + 0x9E3779B9u + (value << 6) + (value >> 2);
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PositiveModulo(int value, int divisor)
        {
            if (divisor <= 0)
                return 0;
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 AupToObserverRelative(
            long sectorX,
            long sectorY,
            long sectorZ,
            float3 local,
            long observerSectorX,
            long observerSectorY,
            long observerSectorZ,
            float3 observerLocal,
            double sectorSizeMeters)
        {
            double3 sectorDelta = new double3(sectorX - observerSectorX, sectorY - observerSectorY, sectorZ - observerSectorZ);
            double3 meters = sectorDelta * sectorSizeMeters + ((double3)local - (double3)observerLocal);
            return new float3((float)meters.x, (float)meters.y, (float)meters.z);
        }
    }

    public static class KineticCharacterRigCsvParser
    {
        public static bool TryApply(ReadOnlySpan<char> csv, ref KineticCharacterTuningDTO tuning, ref KineticCharacterRigDTO rig)
        {
            bool any = false;
            int index = 0;
            while (index < csv.Length)
            {
                ReadOnlySpan<char> line = ReadLine(csv, ref index);
                Trim(ref line);
                if (line.Length == 0 || line[0] == '#')
                    continue;

                int separator = IndexOfSeparator(line);
                if (separator <= 0 || separator >= line.Length - 1)
                    continue;

                ReadOnlySpan<char> key = line.Slice(0, separator);
                ReadOnlySpan<char> valueSpan = line.Slice(separator + 1);
                Trim(ref key);
                Trim(ref valueSpan);
                if (!TryParseFloat(valueSpan, out float value))
                    continue;

                any |= ApplyValue(key, value, ref tuning, ref rig);
            }

            tuning = KineticCharacterSanitizer.SanitizeTuning(tuning);
            rig.ArmUpperLength = KineticCharacterSanitizer.PositiveFinite(rig.ArmUpperLength, 0.34f);
            rig.ArmLowerLength = KineticCharacterSanitizer.PositiveFinite(rig.ArmLowerLength, 0.32f);
            rig.LegUpperLength = KineticCharacterSanitizer.PositiveFinite(rig.LegUpperLength, 0.46f);
            rig.LegLowerLength = KineticCharacterSanitizer.PositiveFinite(rig.LegLowerLength, 0.45f);
            rig.ShoulderWidth = KineticCharacterSanitizer.PositiveFinite(rig.ShoulderWidth, 0.42f);
            rig.HipWidth = KineticCharacterSanitizer.PositiveFinite(rig.HipWidth, 0.32f);
            rig.SpineLength = KineticCharacterSanitizer.PositiveFinite(rig.SpineLength, 0.54f);
            rig.NeckLength = KineticCharacterSanitizer.PositiveFinite(rig.NeckLength, 0.12f);
            return any;
        }

        private static bool ApplyValue(ReadOnlySpan<char> key, float value, ref KineticCharacterTuningDTO tuning, ref KineticCharacterRigDTO rig)
        {
            if (EqualsKey(key, "locomotion_frequency_hz"))
                tuning.LocomotionFrequencyHz = value;
            else if (EqualsKey(key, "locomotion_amplitude_meters"))
                tuning.LocomotionAmplitudeMeters = value;
            else if (EqualsKey(key, "breathing_amplitude_meters"))
                tuning.BreathingAmplitudeMeters = value;
            else if (EqualsKey(key, "breathing_frequency_hz"))
                tuning.BreathingFrequencyHz = value;
            else if (EqualsKey(key, "ik_tolerance_meters"))
                tuning.IkToleranceMeters = value;
            else if (EqualsKey(key, "global_quality_weight"))
                tuning.GlobalQualityWeight = value;
            else if (EqualsKey(key, "minimum_ik_iterations"))
                tuning.MinimumIkIterations = (int)math.round(value);
            else if (EqualsKey(key, "ultra_ik_iterations"))
                tuning.UltraIkIterations = (int)math.round(value);
            else if (EqualsKey(key, "wall_brace_distance_meters"))
                tuning.WallBraceDistanceMeters = value;
            else if (EqualsKey(key, "tool_alignment_weight"))
                tuning.ToolAlignmentWeight = value;
            else if (EqualsKey(key, "damage_flinch_radians"))
                tuning.DamageFlinchRadians = value;
            else if (EqualsKey(key, "damage_flinch_seconds"))
                tuning.DamageFlinchSeconds = value;
            else if (EqualsKey(key, "arm_upper_length"))
                rig.ArmUpperLength = value;
            else if (EqualsKey(key, "arm_lower_length"))
                rig.ArmLowerLength = value;
            else if (EqualsKey(key, "leg_upper_length"))
                rig.LegUpperLength = value;
            else if (EqualsKey(key, "leg_lower_length"))
                rig.LegLowerLength = value;
            else if (EqualsKey(key, "shoulder_width"))
                rig.ShoulderWidth = value;
            else if (EqualsKey(key, "hip_width"))
                rig.HipWidth = value;
            else if (EqualsKey(key, "spine_length"))
                rig.SpineLength = value;
            else if (EqualsKey(key, "neck_length"))
                rig.NeckLength = value;
            else
                return false;

            return true;
        }

        private static ReadOnlySpan<char> ReadLine(ReadOnlySpan<char> text, ref int index)
        {
            int start = index;
            while (index < text.Length && text[index] != '\n' && text[index] != '\r')
                index++;
            int end = index;
            while (index < text.Length && (text[index] == '\n' || text[index] == '\r'))
                index++;
            return text.Slice(start, end - start);
        }

        private static int IndexOfSeparator(ReadOnlySpan<char> line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == ',' || c == '=')
                    return i;
            }

            return -1;
        }

        private static void Trim(ref ReadOnlySpan<char> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start < span.Length && IsSpace(span[start]))
                start++;
            while (end >= start && IsSpace(span[end]))
                end--;
            span = start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<char>.Empty;
        }

        private static bool TryParseFloat(ReadOnlySpan<char> span, out float value)
        {
            value = 0f;
            if (span.Length == 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (span[index] == '-')
            {
                sign = -1f;
                index++;
            }
            else if (span[index] == '+')
            {
                index++;
            }

            bool any = false;
            double result = 0d;
            while (index < span.Length && span[index] >= '0' && span[index] <= '9')
            {
                result = result * 10d + (span[index] - '0');
                any = true;
                index++;
            }

            if (index < span.Length && span[index] == '.')
            {
                index++;
                double scale = 0.1d;
                while (index < span.Length && span[index] >= '0' && span[index] <= '9')
                {
                    result += (span[index] - '0') * scale;
                    scale *= 0.1d;
                    any = true;
                    index++;
                }
            }

            if (!any)
                return false;

            value = (float)(result * sign);
            return math.isfinite(value);
        }

        private static bool EqualsKey(ReadOnlySpan<char> left, string right)
        {
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                char a = ToLowerAscii(left[i]);
                char b = ToLowerAscii(right[i]);
                if (a != b)
                    return false;
            }

            return true;
        }

        private static char ToLowerAscii(char c)
        {
            return c >= 'A' && c <= 'Z' ? (char)(c + 32) : c;
        }

        private static bool IsSpace(char c)
        {
            return c == ' ' || c == '\t';
        }
    }

    public static class KineticCharacterBlackBox
    {
        public static bool TryDumpTelemetry(string projectRoot, NativeArray<KineticAnimationTelemetryEntry> telemetry, NativeArray<int> cursor)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            try
            {
                string root = string.IsNullOrEmpty(projectRoot) ? "." : projectRoot;
                string path = Path.Combine(root, KineticCharacterAnimatorConstants.DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                int start = cursor.IsCreated && cursor.Length > 0
                    ? KineticCharacterMath.PositiveModulo(cursor[0], telemetry.Length)
                    : 0;
                for (int i = 0; i < telemetry.Length; i++)
                {
                    KineticAnimationTelemetryEntry entry = telemetry[KineticCharacterMath.PositiveModulo(start + i, telemetry.Length)];
                    writer.Write(entry.RootSectorX);
                    writer.Write(entry.RootSectorY);
                    writer.Write(entry.RootSectorZ);
                    writer.Write(entry.RootLocal.x);
                    writer.Write(entry.RootLocal.y);
                    writer.Write(entry.RootLocal.z);
                    writer.Write(entry.Frame);
                    writer.Write(entry.BonesEvaluated);
                    writer.Write(entry.AverageIkIterations);
                    writer.Write(entry.CpuTimeMicroseconds);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.Flags);
                    writer.Write(entry.GlobalQualityWeight);
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
        }
    }
}
