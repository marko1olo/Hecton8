using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.UI
{
    public static class TerminalOsConstants
    {
        public const int TerminalCapacity = 64;
        public const int ActiveTargetTerminals = 50;
        public const int TerminalStateStrideBytes = 48;
        public const int ScreenCommandStrideBytes = 16;
        public const int TerminalInteractionStrideBytes = 32;
        public const int TerminalInputStateStrideBytes = 64;
        public const int TerminalInputGpuStateStrideBytes = 32;
        public const int TerminalInputTelemetryStrideBytes = 64;
        public const int TerminalInputTuningStrideBytes = 64;
        public const int TerminalPlaneStrideBytes = 128;
        public const int GazeRayStrideBytes = 80;
        public const int ButtonAabbStrideBytes = 32;
        public const int FixedStringPayloadOffsetBytes = 2;
        public const int MaxFixedStringPayloadBytes = 30;
        public const int GlyphCount = 256;
        public const int BlackBoxFrameCount = 300;
        public const int MaxQueuedClicks = 64;
        public const int ButtonAabbCapacity = TerminalCapacity * 2;
        public const uint PlaneFlagActive = 1u << 0;
        public const uint PlaneFlagPowered = 1u << 1;
        public const uint PlaneFlagSubmerged = 1u << 2;
        public const uint InteractionFlagHover = 1u << 0;
        public const uint InteractionFlagPress = 1u << 1;
        public const uint InteractionFlagHold = 1u << 2;
        public const uint InteractionFlagRelease = 1u << 3;
        public const uint InteractionFlagScroll = 1u << 4;
        public const uint InteractionFlagCandidate = 1u << 8;
        public const uint InteractionFlagCulled = 1u << 9;
        public const uint InteractionFlagInactive = 1u << 10;
        public const uint InteractionFlagNonFinite = 1u << 11;
        public const uint ButtonFlagEnabled = 1u << 0;
        public const byte InteractionUiStateShow = 1;
        public const byte InteractionUiFlagTerminal = 1 << 0;
        public const uint CommandOpenDoor = 0x4F504452u; // OPDR
        public const uint CommandAcknowledge = 0x41434B30u; // ACK0
        public const uint TerminalHashSeed = 0x5445524Du; // TERM
        public const int DecryptionPuzzleStrideBytes = 32;
        public const int DecryptionTerminalStrideBytes = 64;
        public const int DecryptionKnobInputStrideBytes = 64;
        public const int DecryptionTelemetryStrideBytes = 64;
        public const uint DecryptionFlagActive = 1u << 0;
        public const uint DecryptionFlagSolved = 1u << 1;
        public const uint DecryptionFlagInitialized = 1u << 2;
        public const uint DecryptionFlagNonFinite = 1u << 3;
        public const uint DecryptionFlagInteractionBlocked = 1u << 4;
        public const uint DecryptionHoldFrameMask = 0xFFFF0000u;
        public const int DecryptionHoldFrameShift = 16;
        public const int DecryptionRequiredHoldFrames = 30;
        public const float DecryptionSolveThreshold01 = 0.98f;
        public const uint DecryptionKnobFlagActive = 1u << 0;
        public const uint DecryptionKnobFlagGrab = 1u << 1;
        public const uint DecryptionKnobFlagFrequency = 1u << 2;
        public const uint DecryptionKnobFlagPhase = 1u << 3;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.TerminalStateStrideBytes)]
    public struct TerminalStateDTO
    {
        [FieldOffset(0)] public uint TerminalHash;
        // GPU ABI: byte 7 is the CPU dirty flag packed into the unused alpha byte; TerminalBlit.compute masks RGB only.
        [FieldOffset(4)] public uint BackgroundColor;
        [FieldOffset(7)] public byte IsDirty;
        [FieldOffset(8)] public float Value1;
        [FieldOffset(12)] public float Value2;
        [FieldOffset(16)] public FixedString32Bytes TextLine;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.ScreenCommandStrideBytes)]
    public struct ScreenCommandDTO
    {
        [FieldOffset(0)] public uint FontAtlasUV_Packed;
        [FieldOffset(4)] public float2 Position;
        [FieldOffset(12)] public float Scale;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.ButtonAabbStrideBytes)]
    public struct ButtonAABBDTO
    {
        [FieldOffset(0)] public float4 RectUv;
        [FieldOffset(16)] public uint TerminalHash;
        [FieldOffset(20)] public uint CommandHash;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.TerminalInteractionStrideBytes)]
    public struct TerminalInteractionDTO
    {
        [FieldOffset(0)] public uint TerminalHash;
        [FieldOffset(4)] public float2 LocalHitUV;
        [FieldOffset(12)] public uint InteractionFlags;
        [FieldOffset(16)] public float Distance;
        [FieldOffset(20)] private byte _pad0;
        [FieldOffset(21)] private byte _pad1;
        [FieldOffset(22)] private byte _pad2;
        [FieldOffset(23)] private byte _pad3;
        [FieldOffset(24)] private byte _pad4;
        [FieldOffset(25)] private byte _pad5;
        [FieldOffset(26)] private byte _pad6;
        [FieldOffset(27)] private byte _pad7;
        [FieldOffset(28)] private byte _pad8;
        [FieldOffset(29)] private byte _pad9;
        [FieldOffset(30)] private byte _pad10;
        [FieldOffset(31)] private byte _pad11;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.TerminalPlaneStrideBytes)]
    public struct TerminalPlaneDTO
    {
        [FieldOffset(0)] public AbsoluteUniversePosition CenterAup;
        [FieldOffset(48)] public float3 Normal;
        [FieldOffset(60)] public float3 Up;
        [FieldOffset(72)] public float3 Right;
        [FieldOffset(84)] public float Width;
        [FieldOffset(88)] public float Height;
        [FieldOffset(92)] public uint TerminalHash;
        [FieldOffset(96)] public uint Flags;
        [FieldOffset(100)] public uint LayoutFirstButton;
        [FieldOffset(104)] public uint LayoutButtonCount;
        [FieldOffset(108)] public float Power01;
        [FieldOffset(112)] public float Submerged01;
        [FieldOffset(116)] private uint _pad0;
        [FieldOffset(120)] private uint _pad1;
        [FieldOffset(124)] private uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.GazeRayStrideBytes)]
    public struct GazeRayDTO
    {
        [FieldOffset(0)] public AbsoluteUniversePosition OriginAup;
        [FieldOffset(48)] public float3 Direction;
        [FieldOffset(60)] public uint InteractionFlags;
        [FieldOffset(64)] public uint Frame;
        [FieldOffset(68)] public float2 ScrollDelta;
        [FieldOffset(76)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.TerminalInputStateStrideBytes)]
    public struct TerminalInputStateDTO
    {
        [FieldOffset(0)] public double3 TerminalAUP;
        [FieldOffset(24)] public float3 ForwardNormal;
        [FieldOffset(36)] public float3 UpVector;
        [FieldOffset(48)] public float2 ProjectedUV;
        [FieldOffset(56)] public uint TerminalHashID;
        [FieldOffset(60)] public uint InputFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.TerminalInputGpuStateStrideBytes)]
    public struct TerminalInputGpuStateDTO
    {
        [FieldOffset(0)] public float2 ProjectedUV;
        [FieldOffset(8)] public uint TerminalHashID;
        [FieldOffset(12)] public uint InputFlags;
        [FieldOffset(16)] public float4 Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.TerminalInputTuningStrideBytes)]
    public struct TerminalInputTuningDTO
    {
        [FieldOffset(0)] public float MaxInteractionDistanceMeters;
        [FieldOffset(4)] public float CursorSnappingTolerance;
        [FieldOffset(8)] public float RaycastThickness;
        [FieldOffset(12)] public float QualityCurvePower;
        [FieldOffset(16)] public float LowRadiusMeters;
        [FieldOffset(20)] public float UltraRadiusMeters;
        [FieldOffset(24)] public uint TuningFlags;
        [FieldOffset(28)] private uint _pad0;
        [FieldOffset(32)] private uint _pad1;
        [FieldOffset(36)] private uint _pad2;
        [FieldOffset(40)] private uint _pad3;
        [FieldOffset(44)] private uint _pad4;
        [FieldOffset(48)] private uint _pad5;
        [FieldOffset(52)] private uint _pad6;
        [FieldOffset(56)] private uint _pad7;
        [FieldOffset(60)] private uint _pad8;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public partial struct MockPowerStateSignal
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public float MockPowerLevel;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public partial struct MockDamageScalarSignal
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public float Damage01;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct MockPowerStatusSignal
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint PoweredMask0;
        [FieldOffset(8)] public uint PoweredMask1;
        [FieldOffset(12)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct TerminalClickSignal : Hecton8.Core.Contracts.Signals.ISignal
    {
        public const int ExpectedCapacity = TerminalOsConstants.MaxQueuedClicks;
        public const int MaxFrameSignals = TerminalOsConstants.MaxQueuedClicks;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 0x54434C4Bu; // TCLK

        [FieldOffset(0)]
        public uint TerminalHash;
        [FieldOffset(4)]
        public float2 LocalUv;
        [FieldOffset(12)]
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct TerminalCommandSignal : Hecton8.Core.Contracts.Signals.ISignal
    {
        public const int ExpectedCapacity = TerminalOsConstants.MaxQueuedClicks;
        public const int MaxFrameSignals = TerminalOsConstants.MaxQueuedClicks;
        public const int LowTierFrameSignals = 16;
        public const uint LaneHash = 0x54434D44u; // TCMD

        [FieldOffset(0)]
        public uint TerminalHash;
        [FieldOffset(4)]
        public uint CommandHash;
        [FieldOffset(8)]
        public float2 LocalUv;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.DecryptionPuzzleStrideBytes)]
    public struct DecryptionPuzzleDTO
    {
        [FieldOffset(0)] public float PlayerFrequency;
        [FieldOffset(4)] public float PlayerPhase;
        [FieldOffset(8)] public float TargetFrequency;
        [FieldOffset(12)] public float TargetPhase;
        [FieldOffset(16)] public float AlignmentAccuracy01;
        [FieldOffset(20)] public uint PuzzleID;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.DecryptionTerminalStrideBytes)]
    public struct DecryptionTerminalDTO
    {
        [FieldOffset(0)] public double3 TerminalAupMeters;
        [FieldOffset(24)] public uint TerminalHash;
        [FieldOffset(28)] public uint NodeHash;
        [FieldOffset(32)] public float InteractionRadiusMeters;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] private uint _pad0;
        [FieldOffset(44)] private uint _pad1;
        [FieldOffset(48)] private uint _pad2;
        [FieldOffset(52)] private uint _pad3;
        [FieldOffset(56)] private uint _pad4;
        [FieldOffset(60)] private uint _pad5;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.DecryptionKnobInputStrideBytes)]
    public struct DecryptionKnobInputDTO
    {
        [FieldOffset(0)] public double3 PlayerAupMeters;
        [FieldOffset(24)] public uint TerminalHash;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float FrequencyDelta;
        [FieldOffset(36)] public float PhaseDelta;
        [FieldOffset(40)] public float DeltaTime;
        [FieldOffset(44)] public uint Frame;
        [FieldOffset(48)] private uint _pad0;
        [FieldOffset(52)] private uint _pad1;
        [FieldOffset(56)] private uint _pad2;
        [FieldOffset(60)] private uint _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct TerminalUnlockedSignal : Hecton8.Core.Contracts.Signals.ISignal
    {
        public const int ExpectedCapacity = TerminalOsConstants.TerminalCapacity;
        public const int MaxFrameSignals = TerminalOsConstants.TerminalCapacity;
        public const int LowTierFrameSignals = 8;
        public const uint LaneHash = 0x5444554Eu; // TDUN

        [FieldOffset(0)] public uint PuzzleID;
        [FieldOffset(4)] public uint NodeHash;
        [FieldOffset(8)] public uint TerminalHash;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public float AlignmentAccuracy01;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] private uint _pad0;
        [FieldOffset(28)] private uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.DecryptionTelemetryStrideBytes)]
    public struct DecryptionTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint PuzzleID;
        [FieldOffset(8)] public float PlayerFrequency;
        [FieldOffset(12)] public float PlayerPhase;
        [FieldOffset(16)] public float TargetFrequency;
        [FieldOffset(20)] public float TargetPhase;
        [FieldOffset(24)] public float AlignmentAccuracy01;
        [FieldOffset(28)] public float BurstMicroseconds;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint NodeHash;
        [FieldOffset(40)] public uint TerminalHash;
        [FieldOffset(44)] public uint FaultFlags;
        [FieldOffset(48)] private uint _pad0;
        [FieldOffset(52)] private uint _pad1;
        [FieldOffset(56)] private uint _pad2;
        [FieldOffset(60)] private uint _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct TerminalPanelInstanceDTO
    {
        [FieldOffset(0)] public float4x4 LocalToWorld;
        [FieldOffset(64)] public float4 SliceFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TerminalTelemetryEntry
    {
        [FieldOffset(0)] public int Frame;
        [FieldOffset(4)] public int TerminalCount;
        [FieldOffset(8)] public int DirtyCount;
        [FieldOffset(12)] public int DispatchedCount;
        [FieldOffset(16)] public float FormatMainThreadMilliseconds;
        [FieldOffset(20)] public float UploadMicroseconds;
        [FieldOffset(24)] public float DispatchMicroseconds;
        [FieldOffset(28)] public uint FaultFlags;
        [FieldOffset(32)] public uint LayoutHash;
        [FieldOffset(36)] public uint HoveredTerminalHash;
        [FieldOffset(40)] public float LastPower01;
        [FieldOffset(44)] public float LastDamage01;
        [FieldOffset(48)] public int EvaluatedTerminals;
        [FieldOffset(52)] public int FramesBetweenUpdates;
        [FieldOffset(56)] public float IntersectionMicroseconds;
        [FieldOffset(60)] public float GlobalQualityWeight;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.TerminalInputTelemetryStrideBytes)]
    public struct TerminalInputTelemetryEntry
    {
        [FieldOffset(0)] public int Frame;
        [FieldOffset(4)] public int EvaluatedTerminals;
        [FieldOffset(8)] public int SuccessfulProjections;
        [FieldOffset(12)] public int SignalsDispatched;
        [FieldOffset(16)] public float BurstMicroseconds;
        [FieldOffset(20)] public float EvalRadiusMeters;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public uint FaultFlags;
        [FieldOffset(32)] public uint HotPathAllocBytes;
        [FieldOffset(36)] public uint RollbackExcluded;
        [FieldOffset(40)] public uint LastHoveredTerminalHash;
        [FieldOffset(44)] public float CursorSnappingTolerance;
        [FieldOffset(48)] public float RaycastThickness;
        [FieldOffset(52)] public int NonFiniteCount;
        [FieldOffset(56)] private uint _pad0;
        [FieldOffset(60)] private uint _pad1;
    }

    public static class TerminalOsHash
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashIndex(int index)
        {
            uint hash = TerminalOsConstants.TerminalHashSeed;
            hash = (hash ^ (uint)(index + 1)) * 16777619u;
            hash = (hash ^ ((uint)index << 16)) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1A(byte value, uint hash)
        {
            return (hash ^ value) * 16777619u;
        }
    }

    public struct MockTerminalDataGenerator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ResolvePower01(uint frame)
        {
            uint period = frame % 720u;
            uint mirrored = period <= 360u ? period : 720u - period;
            return math.saturate((float)mirrored * (1f / 360f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ResolveDamage01(uint frame)
        {
            uint period = (frame + 91u) % 512u;
            uint ramp = period <= 256u ? period : 512u - period;
            float baseDamage = (float)ramp * (1f / 256f);
            return baseDamage > 0.55f ? baseDamage : baseDamage * 0.35f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MockPowerStatusSignal ResolvePowerStatus(uint frame, float power01)
        {
            uint outagePhase = (frame / 300u) & 3u;
            uint mask0 = 0xFFFFFFFFu;
            uint mask1 = 0xFFFFFFFFu;
            if (power01 < 0.08f || outagePhase == 2u)
            {
                mask0 = 0x00000000u;
                mask1 = 0x00000000u;
            }
            else if (outagePhase == 1u)
            {
                mask0 = 0xF7F7F7F7u;
                mask1 = 0x7F7F7F7Fu;
            }

            return new MockPowerStatusSignal
            {
                Frame = frame,
                PoweredMask0 = mask0,
                PoweredMask1 = mask1
            };
        }
    }

    public static class TerminalAsciiFormatter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WritePowerLine(ref FixedString32Bytes text, int powerPercent, int pressurePercent, bool powered)
        {
            text.Clear();
            if (!powered)
                return;

            AppendAscii(ref text, (byte)'P');
            AppendAscii(ref text, (byte)'W');
            AppendAscii(ref text, (byte)'R');
            AppendAscii(ref text, (byte)' ');
            AppendUnsignedAscii(ref text, (uint)math.clamp(powerPercent, 0, 100), 3);
            AppendAscii(ref text, (byte)'%');
            AppendAscii(ref text, (byte)' ');
            AppendAscii(ref text, (byte)'P');
            AppendAscii(ref text, (byte)'S');
            AppendAscii(ref text, (byte)'I');
            AppendAscii(ref text, (byte)' ');
            AppendUnsignedAscii(ref text, (uint)math.clamp(pressurePercent, 0, 999), 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AppendUnsignedAscii(ref FixedString32Bytes text, uint value, int minDigits)
        {
            uint hundreds = value / 100u;
            uint tens = (value / 10u) % 10u;
            uint ones = value % 10u;

            if (hundreds > 0u || minDigits >= 3)
                AppendAscii(ref text, (byte)((byte)'0' + hundreds));

            if (hundreds > 0u || tens > 0u || minDigits >= 2)
                AppendAscii(ref text, (byte)((byte)'0' + tens));

            AppendAscii(ref text, (byte)((byte)'0' + ones));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AppendAscii(ref FixedString32Bytes text, byte value)
        {
            if (text.Length < TerminalOsConstants.MaxFixedStringPayloadBytes)
                text.Add(value);
        }
    }

    public static class TerminalOsSelfAudit
    {
        public static bool ValidateLayoutAndRayPlaneMath()
        {
            if (UnsafeUtility.SizeOf<TerminalInteractionDTO>() != TerminalOsConstants.TerminalInteractionStrideBytes ||
                UnsafeUtility.SizeOf<TerminalInputStateDTO>() != TerminalOsConstants.TerminalInputStateStrideBytes ||
                UnsafeUtility.SizeOf<TerminalInputGpuStateDTO>() != TerminalOsConstants.TerminalInputGpuStateStrideBytes ||
                UnsafeUtility.SizeOf<TerminalPlaneDTO>() != TerminalOsConstants.TerminalPlaneStrideBytes ||
                UnsafeUtility.SizeOf<GazeRayDTO>() != TerminalOsConstants.GazeRayStrideBytes ||
                UnsafeUtility.SizeOf<ButtonAABBDTO>() != TerminalOsConstants.ButtonAabbStrideBytes ||
                UnsafeUtility.SizeOf<DecryptionPuzzleDTO>() != TerminalOsConstants.DecryptionPuzzleStrideBytes ||
                UnsafeUtility.SizeOf<DecryptionTerminalDTO>() != TerminalOsConstants.DecryptionTerminalStrideBytes ||
                UnsafeUtility.SizeOf<DecryptionKnobInputDTO>() != TerminalOsConstants.DecryptionKnobInputStrideBytes ||
                UnsafeUtility.SizeOf<TerminalUnlockedSignal>() != 32 ||
                UnsafeUtility.SizeOf<DecryptionTelemetryEntry>() != TerminalOsConstants.DecryptionTelemetryStrideBytes ||
                UnsafeUtility.SizeOf<TerminalInputTelemetryEntry>() != TerminalOsConstants.TerminalInputTelemetryStrideBytes ||
                UnsafeUtility.SizeOf<TerminalInputTuningDTO>() != TerminalOsConstants.TerminalInputTuningStrideBytes ||
                UnsafeUtility.SizeOf<TerminalTelemetryEntry>() != 64)
            {
                return false;
            }

            TerminalPlaneDTO plane = new TerminalPlaneDTO
            {
                CenterAup = new AbsoluteUniversePosition { LocalZ = 2f },
                Normal = new float3(0f, 0f, -1f),
                Up = new float3(0f, 1f, 0f),
                Right = new float3(1f, 0f, 0f),
                Width = 2f,
                Height = 2f,
                TerminalHash = 1u,
                Flags = TerminalOsConstants.PlaneFlagActive | TerminalOsConstants.PlaneFlagPowered
            };
            GazeRayDTO gaze = new GazeRayDTO
            {
                OriginAup = default,
                Direction = new float3(0f, 0f, 1f)
            };

            float3 centerFromOrigin = AupPrecisionMath.DowncastLocalDelta(
                AbsoluteUniversePosition.DeltaMetersClamped(in plane.CenterAup, in gaze.OriginAup),
                float3.zero);
            float denom = math.dot(gaze.Direction, plane.Normal);
            float safeDenomMagnitude = math.max(math.abs(denom), 0.0001f);
            float safeDenom = math.select(-safeDenomMagnitude, safeDenomMagnitude, denom >= 0f);
            float distance = math.dot(centerFromOrigin, plane.Normal) / safeDenom;
            float3 local = gaze.Direction * distance - centerFromOrigin;
            float2 uv = new float2(
                math.dot(local, plane.Right) / math.max(plane.Width, 0.001f) + 0.5f,
                math.dot(local, plane.Up) / math.max(plane.Height, 0.001f) + 0.5f);

            return math.all(math.isfinite(uv)) &&
                   math.abs(distance - 2f) < 0.0001f &&
                   math.abs(uv.x - 0.5f) < 0.0001f &&
                   math.abs(uv.y - 0.5f) < 0.0001f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct UpdateTerminalTextJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1: States is a TerminalOS-owned Vault buffer pointer resolved immediately before scheduling by the owner phase. The job does not retain the pointer after execution.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2: Each parallel worker writes only `States[index]`; signal inputs are read-only NativeArrays from distinct Vault lanes and are marked NoAlias.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3: Completion is owned by TerminalOsRuntime through dispatcher fence finalization before upload/readback paths inspect the mutated rows.
        [NativeDisableUnsafePtrRestriction] [NoAlias] public TerminalStateDTO* States;
        [ReadOnly] [NoAlias] public NativeArray<MockPowerStateSignal> PowerSignals;
        [ReadOnly] [NoAlias] public NativeArray<MockDamageScalarSignal> DamageSignals;
        [ReadOnly] [NoAlias] public NativeArray<MockPowerStatusSignal> PowerStatusSignals;
        public int TerminalCount;
        public uint Frame;

        public void Execute(int index)
        {
            if (index < 0 || index >= TerminalCount || States == null)
                return;

            ref TerminalStateDTO state = ref UnsafeUtility.AsRef<TerminalStateDTO>(States + index);
            MockPowerStateSignal power = PowerSignals.IsCreated && PowerSignals.Length > 0
                ? PowerSignals[0]
                : default;
            MockDamageScalarSignal damage = DamageSignals.IsCreated && DamageSignals.Length > 0
                ? DamageSignals[0]
                : default;
            MockPowerStatusSignal status = PowerStatusSignals.IsCreated && PowerStatusSignals.Length > 0
                ? PowerStatusSignals[0]
                : default;

            float perTerminalBias = ((index * 17 + (int)(Frame & 31u)) & 31) * (1f / 620f);
            float power01 = math.saturate(power.MockPowerLevel * 0.01f - perTerminalBias);
            float damage01 = math.saturate(damage.Damage01 + (((index * 13) & 7) * 0.025f));
            bool powered = index < 32
                ? ((status.PoweredMask0 & (1u << index)) != 0u)
                : ((status.PoweredMask1 & (1u << (index - 32))) != 0u);

            int powerPercent = (int)math.round(power01 * 100f);
            int previousPercent = (int)math.round(math.saturate(state.Value1) * 100f);
            int damagePercent = (int)math.round(damage01 * 100f);
            int previousDamagePercent = (int)math.round(math.saturate(state.Value2) * 100f);
            byte previousPowered = (state.BackgroundColor & 0x00FFFFFFu) == 0u ? (byte)0 : (byte)1;
            byte pendingDirty = state.IsDirty;

            if (!powered)
            {
                state.Value1 = 0f;
                state.Value2 = damage01;
                state.BackgroundColor = 0u;
                state.IsDirty = pendingDirty;
                TerminalAsciiFormatter.WritePowerLine(ref state.TextLine, 0, damagePercent, false);
                if (previousPowered != 0 || previousPercent != 0 || previousDamagePercent != damagePercent)
                    state.IsDirty = 1;
                return;
            }

            uint background = damage01 > 0.5f ? 0x00101830u : 0x00061418u;
            state.Value1 = power01;
            state.Value2 = damage01;
            state.BackgroundColor = background;
            state.IsDirty = pendingDirty;
            TerminalAsciiFormatter.WritePowerLine(ref state.TextLine, powerPercent, damagePercent, true);
            if (previousPowered == 0 || previousPercent != powerPercent || previousDamagePercent != damagePercent)
                state.IsDirty = 1;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct TerminalClickResolveJob : IJobParallelFor
    {
        [ReadOnly] [NoAlias] public NativeArray<TerminalClickSignal>.ReadOnly Clicks;
        [ReadOnly] [NoAlias] public NativeArray<ButtonAABBDTO> Buttons;
        public int ClickCount;
        public int ButtonCount;
        public global::Hecton8.Core.MpscSignalRingBuffer<TerminalCommandSignal>.ParallelWriter Commands;
        [NativeDisableParallelForRestriction] public NativeArray<int> CommandsBudget;
        public void Execute(int index)
        {
            if (index < 0 || index >= ClickCount)
                return;

            TerminalClickSignal click = Clicks[index];
            float2 uv = click.LocalUv;
            if (!math.all(math.isfinite(uv)))
                return;

            for (int i = 0; i < ButtonCount; i++)
            {
                ButtonAABBDTO button = Buttons[i];
                if (button.TerminalHash != click.TerminalHash)
                    continue;
                if ((button.Flags & TerminalOsConstants.ButtonFlagEnabled) == 0u)
                    continue;

                float4 rect = button.RectUv;
                bool inside = uv.x >= rect.x && uv.y >= rect.y && uv.x <= rect.z && uv.y <= rect.w;
                if (!inside)
                    continue;

                SignalBus<TerminalCommandSignal>.TryEnqueueBounded(Commands, CommandsBudget, new TerminalCommandSignal
                {
                    TerminalHash = click.TerminalHash,
                    CommandHash = button.CommandHash,
                    LocalUv = uv
                });
                return;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MockGazeRayJob : IJob
    {
        [NoAlias] public NativeArray<GazeRayDTO> GazeRays;
        public AbsoluteUniversePosition FallbackOriginAup;
        public float3 FallbackForward;
        public float2 ScrollDelta;
        public uint InteractionFlags;
        public uint Frame;
        public float MicroSwayRadians;

        public void Execute()
        {
            if (!GazeRays.IsCreated || GazeRays.Length == 0)
                return;

            float phase = (Frame & 1023u) * (1f / 1024f);
            float sway = TriangleWave(phase) * math.max(0f, MicroSwayRadians);
            float3 forward = math.normalizesafe(FallbackForward, new float3(0f, 0f, 1f));
            float3 side = math.normalizesafe(math.cross(new float3(0f, 1f, 0f), forward), new float3(1f, 0f, 0f));
            float3 direction = math.normalizesafe(forward + side * sway, forward);
            GazeRays[0] = new GazeRayDTO
            {
                OriginAup = FallbackOriginAup,
                Direction = direction,
                InteractionFlags = InteractionFlags,
                Frame = Frame,
                ScrollDelta = ScrollDelta
            };
        }

        private static float TriangleWave(float phase)
        {
            return math.abs(math.frac(phase) * 2f - 1f) * 2f - 1f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockGazeVectorsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<GazeRayDTO> GazeRays;
        public AbsoluteUniversePosition FallbackOriginAup;
        public float3 FallbackForward;
        public float2 ScrollDelta;
        public uint InteractionFlags;
        public uint Frame;
        public float MicroSwayRadians;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)GazeRays.Length)
                return;

            float framePhase = ((Frame + (uint)(index * 37)) & 2047u) * (1f / 2048f);
            float swayScale = math.max(0f, MicroSwayRadians);
            float swayX = TriangleWave(framePhase) * swayScale;
            float swayY = TriangleWave(framePhase * 1.6180339f + 0.37f) * swayScale * 0.5f;
            float3 forward = math.normalizesafe(FallbackForward, new float3(0f, 0f, 1f));
            float3 side = math.normalizesafe(math.cross(new float3(0f, 1f, 0f), forward), new float3(1f, 0f, 0f));
            float3 up = math.normalizesafe(math.cross(forward, side), new float3(0f, 1f, 0f));
            float3 direction = math.normalizesafe(forward + side * swayX + up * swayY, forward);
            GazeRays[index] = new GazeRayDTO
            {
                OriginAup = FallbackOriginAup,
                Direction = direction,
                InteractionFlags = InteractionFlags,
                Frame = Frame,
                ScrollDelta = ScrollDelta
            };
        }

        private static float TriangleWave(float phase)
        {
            return math.abs(math.frac(phase) * 2f - 1f) * 2f - 1f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CullTerminalsJob : IJobParallelFor
    {
        [ReadOnly] [NoAlias] public NativeArray<TerminalPlaneDTO> Planes;
        [ReadOnly] [NoAlias] public NativeArray<GazeRayDTO> GazeRays;
        [NoAlias] public NativeArray<TerminalInteractionDTO> Interactions;
        public int TerminalCount;
        public float MaxDistanceMeters;
        public float ViewConeCos;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)TerminalCount || !GazeRays.IsCreated || GazeRays.Length == 0)
                return;

            TerminalPlaneDTO plane = Planes[index];
            GazeRayDTO gaze = GazeRays[0];
            TerminalInteractionDTO result = default;
            result.TerminalHash = plane.TerminalHash;
            result.Distance = float.MaxValue;

            if ((plane.Flags & TerminalOsConstants.PlaneFlagActive) == 0u ||
                (plane.Flags & TerminalOsConstants.PlaneFlagPowered) == 0u ||
                (plane.Flags & TerminalOsConstants.PlaneFlagSubmerged) != 0u)
            {
                result.InteractionFlags = TerminalOsConstants.InteractionFlagInactive;
                Interactions[index] = result;
                return;
            }

            float3 delta = AupPrecisionMath.DowncastLocalDelta(
                AbsoluteUniversePosition.DeltaMetersClamped(in plane.CenterAup, in gaze.OriginAup),
                float3.zero);
            if (!math.all(math.isfinite(delta)) || !math.all(math.isfinite(gaze.Direction)))
            {
                result.InteractionFlags = TerminalOsConstants.InteractionFlagNonFinite;
                Interactions[index] = result;
                return;
            }

            float distanceSq = math.lengthsq(delta);
            float maxDistance = math.isfinite(MaxDistanceMeters) ? math.max(0.1f, MaxDistanceMeters) : 0.1f;
            if (!math.isfinite(distanceSq) || distanceSq > maxDistance * maxDistance)
            {
                result.InteractionFlags = TerminalOsConstants.InteractionFlagCulled;
                Interactions[index] = result;
                return;
            }

            float3 toTerminal = math.normalizesafe(delta, gaze.Direction);
            if (math.dot(gaze.Direction, toTerminal) < ViewConeCos)
            {
                result.InteractionFlags = TerminalOsConstants.InteractionFlagCulled;
                Interactions[index] = result;
                return;
            }

            result.InteractionFlags = TerminalOsConstants.InteractionFlagCandidate;
            result.Distance = SafeDistanceFromSq(distanceSq);
            Interactions[index] = result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SafeDistanceFromSq(float distanceSq)
        {
            return math.select(
                0f,
                distanceSq * math.rsqrt(math.max(distanceSq, 0.000001f)),
                distanceSq > 0f && math.isfinite(distanceSq));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct TerminalIntersectionJob : IJobParallelFor
    {
        [ReadOnly] [NoAlias] public NativeArray<TerminalPlaneDTO> Planes;
        [ReadOnly] [NoAlias] public NativeArray<GazeRayDTO> GazeRays;
        [NoAlias] public NativeArray<TerminalInteractionDTO> Interactions;
        public int TerminalCount;
        public float MaxDistanceMeters;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)TerminalCount || !GazeRays.IsCreated || GazeRays.Length == 0)
                return;

            TerminalInteractionDTO current = Interactions[index];
            if ((current.InteractionFlags & TerminalOsConstants.InteractionFlagCandidate) == 0u)
                return;

            TerminalPlaneDTO plane = Planes[index];
            GazeRayDTO gaze = GazeRays[0];
            bool rawFinite =
                math.all(math.isfinite(plane.Normal)) &&
                math.all(math.isfinite(plane.Right)) &&
                math.all(math.isfinite(plane.Up)) &&
                math.all(math.isfinite(gaze.Direction));
            float3 normal = math.normalizesafe(plane.Normal, new float3(0f, 0f, -1f));
            float3 right = math.normalizesafe(plane.Right, new float3(1f, 0f, 0f));
            float3 up = math.normalizesafe(plane.Up, new float3(0f, 1f, 0f));
            float3 direction = math.normalizesafe(gaze.Direction, new float3(0f, 0f, 1f));
            float3 centerFromOrigin = AupPrecisionMath.DowncastLocalDelta(
                AbsoluteUniversePosition.DeltaMetersClamped(in plane.CenterAup, in gaze.OriginAup),
                float3.zero);
            float denom = math.dot(direction, normal);

            current.TerminalHash = plane.TerminalHash;
            current.LocalHitUV = default;
            current.Distance = float.MaxValue;
            current.InteractionFlags &= TerminalOsConstants.InteractionFlagCandidate;

            if (!rawFinite ||
                !math.all(math.isfinite(centerFromOrigin)) ||
                !math.all(math.isfinite(normal)) ||
                !math.all(math.isfinite(direction)) ||
                math.abs(denom) < 0.00001f)
            {
                current.InteractionFlags |= TerminalOsConstants.InteractionFlagNonFinite;
                Interactions[index] = current;
                return;
            }

            float distance = math.dot(centerFromOrigin, normal) / denom;
            float maxDistance = math.isfinite(MaxDistanceMeters) ? math.max(0.1f, MaxDistanceMeters) : 0.1f;
            if (!math.isfinite(distance) || distance < 0f || distance > maxDistance)
            {
                current.InteractionFlags |= TerminalOsConstants.InteractionFlagCulled;
                Interactions[index] = current;
                return;
            }

            float3 local = direction * distance - centerFromOrigin;
            float width = math.max(0.001f, plane.Width);
            float height = math.max(0.001f, plane.Height);
            float2 uv = new float2(
                math.dot(local, right) / width + 0.5f,
                math.dot(local, up) / height + 0.5f);

            if (!math.all(math.isfinite(uv)) || math.any(uv < 0f) || math.any(uv > 1f))
            {
                current.InteractionFlags |= TerminalOsConstants.InteractionFlagCulled;
                Interactions[index] = current;
                return;
            }

            current.LocalHitUV = uv;
            current.Distance = distance;
            current.InteractionFlags = TerminalOsConstants.InteractionFlagHover |
                                       (gaze.InteractionFlags & (TerminalOsConstants.InteractionFlagPress |
                                                                 TerminalOsConstants.InteractionFlagHold |
                                                                 TerminalOsConstants.InteractionFlagRelease |
                                                                 TerminalOsConstants.InteractionFlagScroll));
            Interactions[index] = current;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateTerminalGazeJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<TerminalPlaneDTO> Planes;
        [ReadOnly, NoAlias] public NativeArray<GazeRayDTO> GazeRays;
        [ReadOnly, NoAlias] public NativeArray<ButtonAABBDTO> Buttons;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1: InputStates points to the Vault-owned SHINOBU_331 terminal projection buffer opened by the TerminalOS owner phase immediately before this owner-scheduled evaluation job is scheduled. The pointer is never retained beyond the copied job value, and each parallel worker writes only row index `index`.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2: The raw pointer route is intentional because the CPU projection DTO carries 64-byte AUP/local-basis state and the task requires UnsafeUtility.AsRef mutation. Shader upload is a later owner-finalized 32-byte TerminalInputGpuStateDTO compaction, not a direct copy of this CPU row.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3: Interactions uses a distinct Vault buffer ID from InputStates, Planes, GazeRays, Buttons, and the upload row-hash lane. TerminalOS owns scheduling and completion, so shader upload and editor readouts occur only after the returned JobHandle is finalized by the owner.
        [NativeDisableUnsafePtrRestriction, NoAlias] public TerminalInputStateDTO* InputStates;
        [NativeDisableUnsafePtrRestriction, NoAlias] public TerminalInteractionDTO* Interactions;
        public int TerminalCount;
        public int ButtonCount;
        public float GlobalQualityWeight;
        public float MaxDistanceMeters;
        public float ViewConeCos;
        public float CursorSnappingTolerance;
        public float RaycastThickness;
        public float QualityCurvePower;
        public float LowRadiusMeters;
        public float UltraRadiusMeters;
        public uint Frame;
        public global::Hecton8.Core.MpscSignalRingBuffer<TerminalCommandSignal>.ParallelWriter Commands;
        [NativeDisableParallelForRestriction] public NativeArray<int> CommandsBudget;
        public global::Hecton8.Core.MpscSignalRingBuffer<InteractionUiSignal>.ParallelWriter UiSignals;
        [NativeDisableParallelForRestriction] public NativeArray<int> UiSignalsBudget;
        public void Execute(int index)
        {
            if ((uint)index >= (uint)TerminalCount ||
                InputStates == null ||
                Interactions == null ||
                !GazeRays.IsCreated ||
                GazeRays.Length == 0)
            {
                return;
            }

            TerminalPlaneDTO plane = Planes[index];
            GazeRayDTO gaze = GazeRays[0];
            ref TerminalInputStateDTO inputState = ref UnsafeUtility.AsRef<TerminalInputStateDTO>(InputStates + index);
            ref TerminalInteractionDTO interaction = ref UnsafeUtility.AsRef<TerminalInteractionDTO>(Interactions + index);
            inputState = default;
            inputState.TerminalAUP = ToAbsoluteDouble3(in plane.CenterAup);
            inputState.ForwardNormal = SanitizeDirection(plane.Normal, new float3(0f, 0f, -1f));
            inputState.UpVector = SanitizeDirection(plane.Up, new float3(0f, 1f, 0f));
            inputState.ProjectedUV = default;
            inputState.TerminalHashID = plane.TerminalHash;
            inputState.InputFlags = TerminalOsConstants.InteractionFlagInactive;

            interaction = default;
            interaction.TerminalHash = plane.TerminalHash;
            interaction.Distance = float.MaxValue;

            if ((plane.Flags & TerminalOsConstants.PlaneFlagActive) == 0u ||
                (plane.Flags & TerminalOsConstants.PlaneFlagPowered) == 0u ||
                (plane.Flags & TerminalOsConstants.PlaneFlagSubmerged) != 0u)
            {
                interaction.InteractionFlags = TerminalOsConstants.InteractionFlagInactive;
                return;
            }

            double3 terminalAup = inputState.TerminalAUP;
            double3 rayAup = ToAbsoluteDouble3(in gaze.OriginAup);
            double3 centerFromOriginDouble = terminalAup - rayAup;
            float3 centerFromOrigin = new float3(
                (float)centerFromOriginDouble.x,
                (float)centerFromOriginDouble.y,
                (float)centerFromOriginDouble.z);
            bool rawFinite =
                math.all(math.isfinite(plane.Normal)) &&
                math.all(math.isfinite(plane.Up)) &&
                math.all(math.isfinite(plane.Right)) &&
                math.all(math.isfinite(gaze.Direction)) &&
                math.all(math.isfinite(centerFromOriginDouble));
            float3 direction = SanitizeDirection(gaze.Direction, new float3(0f, 0f, 1f));
            float3 normal = inputState.ForwardNormal;
            float3 up = inputState.UpVector;

            if (!rawFinite ||
                !math.all(math.isfinite(centerFromOrigin)) ||
                !math.all(math.isfinite(direction)) ||
                !math.all(math.isfinite(normal)) ||
                !math.all(math.isfinite(up)))
            {
                inputState.InputFlags = TerminalOsConstants.InteractionFlagNonFinite;
                interaction.InteractionFlags = TerminalOsConstants.InteractionFlagNonFinite;
                return;
            }

            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f);
            float curvePower = math.clamp(math.isfinite(QualityCurvePower) ? QualityCurvePower : 1f, 0.25f, 4f);
            float curveBlend = math.saturate((curvePower - 0.25f) * (1f / 3.75f));
            float shapedQuality = math.lerp(quality, quality * quality * quality, curveBlend);
            float lowRadius = math.max(0.5f, math.isfinite(LowRadiusMeters) ? LowRadiusMeters : 5f);
            float ultraRadius = math.max(lowRadius, math.isfinite(UltraRadiusMeters) ? UltraRadiusMeters : 25f);
            float qualityRadius = math.lerp(lowRadius, ultraRadius, shapedQuality);
            float configuredRadius = math.isfinite(MaxDistanceMeters) ? math.max(0.5f, MaxDistanceMeters) : qualityRadius;
            float evalRadius = math.min(qualityRadius, configuredRadius);
            float distanceSq = math.lengthsq(centerFromOrigin);
            if (!math.isfinite(distanceSq) || distanceSq > evalRadius * evalRadius)
            {
                inputState.InputFlags = TerminalOsConstants.InteractionFlagCulled;
                interaction.InteractionFlags = TerminalOsConstants.InteractionFlagCulled;
                return;
            }

            float3 toTerminal = math.normalizesafe(centerFromOrigin, direction);
            if (math.dot(direction, toTerminal) < math.clamp(ViewConeCos, -0.5f, 0.95f))
            {
                inputState.InputFlags = TerminalOsConstants.InteractionFlagCulled;
                interaction.InteractionFlags = TerminalOsConstants.InteractionFlagCulled;
                return;
            }

            float denom = math.dot(direction, normal);
            if (!math.isfinite(denom))
            {
                inputState.InputFlags = TerminalOsConstants.InteractionFlagNonFinite;
                interaction.InteractionFlags = TerminalOsConstants.InteractionFlagNonFinite;
                return;
            }

            if (math.abs(denom) < 0.01f)
            {
                inputState.InputFlags = TerminalOsConstants.InteractionFlagCulled;
                interaction.InteractionFlags = TerminalOsConstants.InteractionFlagCulled;
                return;
            }

            float distance = math.dot(centerFromOrigin, normal) / denom;
            if (!math.isfinite(distance) || distance < 0f || distance > evalRadius)
            {
                inputState.InputFlags = TerminalOsConstants.InteractionFlagCulled;
                interaction.InteractionFlags = TerminalOsConstants.InteractionFlagCulled;
                interaction.Distance = math.isfinite(distance) ? distance : float.MaxValue;
                return;
            }

            float3 right = math.normalizesafe(math.cross(up, normal), SanitizeDirection(plane.Right, new float3(1f, 0f, 0f)));
            float3 local = direction * distance - centerFromOrigin;
            float width = math.max(0.001f, plane.Width);
            float height = math.max(0.001f, plane.Height);
            float2 uv = new float2(
                math.dot(local, right) / width + 0.5f,
                math.dot(local, up) / height + 0.5f);

            float edgeTolerance = math.saturate(math.lerp(
                math.max(0f, RaycastThickness),
                math.max(0f, CursorSnappingTolerance),
                quality));
            if (!math.all(math.isfinite(uv)) ||
                uv.x < -edgeTolerance ||
                uv.y < -edgeTolerance ||
                uv.x > 1f + edgeTolerance ||
                uv.y > 1f + edgeTolerance)
            {
                inputState.InputFlags = TerminalOsConstants.InteractionFlagCulled;
                interaction.InteractionFlags = TerminalOsConstants.InteractionFlagCulled;
                interaction.Distance = distance;
                return;
            }

            uint liveFlags = TerminalOsConstants.InteractionFlagHover |
                             (gaze.InteractionFlags & (TerminalOsConstants.InteractionFlagPress |
                                                       TerminalOsConstants.InteractionFlagHold |
                                                       TerminalOsConstants.InteractionFlagRelease |
                                                       TerminalOsConstants.InteractionFlagScroll));
            inputState.ProjectedUV = math.saturate(uv);
            inputState.InputFlags = liveFlags;
            interaction.LocalHitUV = inputState.ProjectedUV;
            interaction.Distance = distance;
            interaction.InteractionFlags = liveFlags;

            DispatchButtonSignals(index, in plane, in interaction);
        }

        private void DispatchButtonSignals(int terminalIndex, in TerminalPlaneDTO plane, in TerminalInteractionDTO interaction)
        {
            float2 uv = interaction.LocalHitUV;
            bool clicked = (interaction.InteractionFlags & TerminalOsConstants.InteractionFlagPress) != 0u;
            int safeButtonCount = math.max(0, ButtonCount);
            int firstButton = (int)math.min(plane.LayoutFirstButton, (uint)safeButtonCount);
            int availableButtons = math.max(0, safeButtonCount - firstButton);
            int localButtonCount = (int)math.min(plane.LayoutButtonCount, (uint)availableButtons);
            int buttonEnd = firstButton + localButtonCount;
            for (int i = firstButton; i < buttonEnd; i++)
            {
                ButtonAABBDTO button = Buttons[i];
                if (button.TerminalHash != interaction.TerminalHash ||
                    (button.Flags & TerminalOsConstants.ButtonFlagEnabled) == 0u)
                {
                    continue;
                }

                float4 rect = button.RectUv;
                float snapTolerance = math.saturate(math.max(0f, CursorSnappingTolerance));
                bool inside = uv.x >= rect.x - snapTolerance &&
                              uv.y >= rect.y - snapTolerance &&
                              uv.x <= rect.z + snapTolerance &&
                              uv.y <= rect.w + snapTolerance;
                if (!inside)
                    continue;

                SignalBus<InteractionUiSignal>.TryEnqueueBounded(UiSignals, UiSignalsBudget, new InteractionUiSignal
                {
                    TargetAup = plane.CenterAup,
                    TargetHash = interaction.TerminalHash,
                    ToolHash = button.CommandHash,
                    State = TerminalOsConstants.InteractionUiStateShow,
                    Flags = TerminalOsConstants.InteractionUiFlagTerminal
                });

                if (clicked)
                {
                    SignalBus<TerminalCommandSignal>.TryEnqueueBounded(Commands, CommandsBudget, new TerminalCommandSignal
                    {
                        TerminalHash = interaction.TerminalHash,
                        CommandHash = button.CommandHash,
                        LocalUv = uv
                    });
                }

                return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeDirection(float3 value, float3 fallback)
        {
            return math.normalizesafe(math.all(math.isfinite(value)) ? value : fallback, fallback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ToAbsoluteDouble3(in AbsoluteUniversePosition aup)
        {
            double cell = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                (aup.GridX * cell) + aup.LocalX,
                (aup.GridY * cell) + aup.LocalY,
                (aup.GridZ * cell) + aup.LocalZ);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockPuzzleDataJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<DecryptionPuzzleDTO> Puzzles;
        [NoAlias] public NativeArray<DecryptionTerminalDTO> Terminals;
        [ReadOnly, NoAlias] public NativeArray<TerminalPlaneDTO> Planes;
        public int PuzzleCount;
        public float BasePlayerFrequency;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)PuzzleCount)
                return;

            TerminalPlaneDTO plane = Planes.IsCreated && index < Planes.Length ? Planes[index] : default;
            uint terminalHash = plane.TerminalHash != 0u ? plane.TerminalHash : TerminalOsHash.HashIndex(index);
            uint seed = terminalHash ^ 0x9E3779B9u ^ ((uint)index * 747796405u);
            float frequencyJitter = ((seed >> 8) & 255u) * (1f / 255f) * 0.35f;
            float phaseJitter = ((seed >> 17) & 255u) * (1f / 255f) * 0.35f;
            Puzzles[index] = new DecryptionPuzzleDTO
            {
                PlayerFrequency = SanitizeRange(BasePlayerFrequency, 0.1f, 12f, 3.25f),
                PlayerPhase = 0.35f,
                TargetFrequency = 4.5f + frequencyJitter,
                TargetPhase = 1.2f + phaseJitter,
                AlignmentAccuracy01 = 0f,
                PuzzleID = terminalHash,
                Flags = TerminalOsConstants.DecryptionFlagActive | TerminalOsConstants.DecryptionFlagInitialized
            };

            Terminals[index] = new DecryptionTerminalDTO
            {
                TerminalAupMeters = ToAbsoluteDouble3(in plane.CenterAup),
                TerminalHash = terminalHash,
                NodeHash = ResolveNodeHash(terminalHash),
                InteractionRadiusMeters = 1.5f,
                Flags = TerminalOsConstants.DecryptionFlagActive | TerminalOsConstants.DecryptionFlagInitialized
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveNodeHash(uint terminalHash)
        {
            uint hash = terminalHash == 0u ? TerminalOsConstants.TerminalHashSeed : terminalHash;
            hash = (hash ^ 0x42415345u) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeRange(float value, float min, float max, float fallback)
        {
            return math.isfinite(value) ? math.clamp(value, min, max) : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ToAbsoluteDouble3(in AbsoluteUniversePosition aup)
        {
            double cell = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                (aup.GridX * cell) + aup.LocalX,
                (aup.GridY * cell) + aup.LocalY,
                (aup.GridZ * cell) + aup.LocalZ);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ClearDecryptionFlagsJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1: Puzzles points to the Vault-owned TerminalDecryptionPuzzles buffer opened by the TerminalOS owner phase immediately before this cold boot job runs. The pointer is never stored outside the job value and the job writes only index-local Flags fields inside [0, PuzzleCount).
        // SAFETY_JUSTIFICATION_PARAGRAPH_2: The alternative NativeArray indexer route was rejected for this specific cold scrub because the XML assignment requires direct unmanaged mutation via raw memory/UnsafeUtility.AsRef for the decryption DTO path. The bounds guard and owner-owned Vault handle are the invariants that replace Unity's container safety metadata.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3: This job is executed with .Run during initialization before any decryption evaluation job is scheduled, so there is no concurrent writer and no alias with the shader upload path. If the pointer is null or index is outside PuzzleCount, the job writes nothing.
        [NativeDisableUnsafePtrRestriction, NoAlias] public DecryptionPuzzleDTO* Puzzles;
        public int PuzzleCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)PuzzleCount || Puzzles == null)
                return;

            ref DecryptionPuzzleDTO puzzle = ref UnsafeUtility.AsRef<DecryptionPuzzleDTO>(Puzzles + index);
            puzzle.Flags = 0u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateDecryptionPipelineJob : IJob
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1: Puzzles points to the Vault-owned TerminalDecryptionPuzzles buffer for one owner-phase scheduled job. TerminalOS does not expose this pointer to consumers; public read/write accessors fail closed while _decryptionScheduled is true, preventing same-frame aliasing against editor reads or target mutation.
        // SAFETY_JUSTIFICATION_PARAGRAPH_2: Raw pointer mutation is retained instead of a NativeArray write-back loop because the prompt explicitly requires unmanaged DTO mutation through UnsafeUtility.AsRef and the DTO is fixed at 32 bytes. The job is fused as a serial IJob so adjacent 32-byte rows are not written by different workers on the same cache line.
        // SAFETY_JUSTIFICATION_PARAGRAPH_3: Terminals and Inputs are read-only, marked NoAlias, and obtained from distinct Vault buffer IDs. The only write outputs are the puzzle buffer and the SignalBus unlock queue; completion is returned as a JobHandle and finalized only by the TerminalOS owner phase.
        [NativeDisableUnsafePtrRestriction, NoAlias] public DecryptionPuzzleDTO* Puzzles;
        [ReadOnly, NoAlias] public NativeArray<DecryptionTerminalDTO> Terminals;
        [ReadOnly, NoAlias] public NativeArray<DecryptionKnobInputDTO> Inputs;
        public int PuzzleCount;
        public float FrequencySensitivity;
        public float PhaseSensitivity;
        public float FreqWeight;
        public float PhaseWeight;
        public float SolveThreshold01;
        public uint Frame;
        public uint StepFrames;
        public global::Hecton8.Core.MpscSignalRingBuffer<TerminalUnlockedSignal>.ParallelWriter UnlockedSignals;
        [NativeDisableParallelForRestriction] public NativeArray<int> UnlockedSignalsBudget;
        public void Execute()
        {
            if (Puzzles == null)
                return;

            int count = math.max(0, PuzzleCount);
            DecryptionKnobInputDTO input = Inputs.IsCreated && Inputs.Length > 0 ? Inputs[0] : default;
            bool inputGrabbed =
                (input.Flags & (TerminalOsConstants.DecryptionKnobFlagActive | TerminalOsConstants.DecryptionKnobFlagGrab)) ==
                (TerminalOsConstants.DecryptionKnobFlagActive | TerminalOsConstants.DecryptionKnobFlagGrab);
            uint stepFrames = math.max(1u, StepFrames);

            for (int index = 0; index < count; index++)
            {
                ref DecryptionPuzzleDTO puzzle = ref UnsafeUtility.AsRef<DecryptionPuzzleDTO>(Puzzles + index);
                if ((puzzle.Flags & TerminalOsConstants.DecryptionFlagActive) == 0u)
                    continue;

                DecryptionTerminalDTO terminal = Terminals.IsCreated && index < Terminals.Length ? Terminals[index] : default;
                if ((puzzle.Flags & TerminalOsConstants.DecryptionFlagSolved) == 0u)
                    ApplyInput(ref puzzle, in terminal, in input, inputGrabbed);

                EvaluateAlignment(ref puzzle);
                EvaluateCompletion(index, ref puzzle, in terminal, stepFrames);
            }
        }

        private void ApplyInput(
            ref DecryptionPuzzleDTO puzzle,
            in DecryptionTerminalDTO terminal,
            in DecryptionKnobInputDTO input,
            bool inputGrabbed)
        {
            if (!inputGrabbed)
                return;

            uint terminalHash = terminal.TerminalHash != 0u ? terminal.TerminalHash : puzzle.PuzzleID;
            if (input.TerminalHash != 0u && input.TerminalHash != terminalHash)
                return;

            double3 deltaDouble = terminal.TerminalAupMeters - input.PlayerAupMeters;
            float3 localDelta = new float3((float)deltaDouble.x, (float)deltaDouble.y, (float)deltaDouble.z);
            float distanceSq = math.lengthsq(localDelta);
            float radius = math.max(0.1f, terminal.InteractionRadiusMeters);
            if (!math.isfinite(distanceSq) || distanceSq > radius * radius)
            {
                puzzle.Flags |= TerminalOsConstants.DecryptionFlagInteractionBlocked;
                return;
            }

            float dt = math.saturate(input.DeltaTime * 60f);
            float frequencyDelta = (input.Flags & TerminalOsConstants.DecryptionKnobFlagFrequency) != 0u
                ? input.FrequencyDelta * FrequencySensitivity * math.max(0.05f, dt)
                : 0f;
            float phaseDelta = (input.Flags & TerminalOsConstants.DecryptionKnobFlagPhase) != 0u
                ? input.PhaseDelta * PhaseSensitivity * math.max(0.05f, dt)
                : 0f;

            puzzle.PlayerFrequency = SanitizeRange(puzzle.PlayerFrequency + frequencyDelta, 0.1f, 12f, 3.25f);
            puzzle.PlayerPhase = WrapPhase(puzzle.PlayerPhase + phaseDelta);
            puzzle.Flags &= ~TerminalOsConstants.DecryptionFlagInteractionBlocked;
        }

        private void EvaluateAlignment(ref DecryptionPuzzleDTO puzzle)
        {
            float playerFrequency = Sanitize(puzzle.PlayerFrequency, 0f);
            float playerPhase = Sanitize(puzzle.PlayerPhase, 0f);
            float targetFrequency = Sanitize(puzzle.TargetFrequency, 0f);
            float targetPhase = Sanitize(puzzle.TargetPhase, 0f);
            float freqDiff = math.abs(playerFrequency - targetFrequency);
            float phaseDiff = math.abs(WrapSignedPhase(playerPhase - targetPhase));
            float alignment = math.saturate(1.0f - (freqDiff * math.max(0f, FreqWeight) + phaseDiff * math.max(0f, PhaseWeight)));
            puzzle.AlignmentAccuracy01 = alignment;
            if (!math.isfinite(alignment))
            {
                puzzle.AlignmentAccuracy01 = 0f;
                puzzle.Flags |= TerminalOsConstants.DecryptionFlagNonFinite;
            }
            else
            {
                puzzle.Flags &= ~TerminalOsConstants.DecryptionFlagNonFinite;
            }
        }

        private void EvaluateCompletion(
            int index,
            ref DecryptionPuzzleDTO puzzle,
            in DecryptionTerminalDTO terminal,
            uint stepFrames)
        {
            if ((puzzle.Flags & TerminalOsConstants.DecryptionFlagSolved) != 0u)
                return;

            uint hold = (puzzle.Flags & TerminalOsConstants.DecryptionHoldFrameMask) >> TerminalOsConstants.DecryptionHoldFrameShift;
            float threshold = SanitizeRange(SolveThreshold01, 0.5f, 0.999f, TerminalOsConstants.DecryptionSolveThreshold01);
            if (puzzle.AlignmentAccuracy01 >= threshold)
                hold = math.min(0xFFFFu, hold + stepFrames);
            else
                hold = 0u;

            uint lowFlags = puzzle.Flags & ~TerminalOsConstants.DecryptionHoldFrameMask;
            puzzle.Flags = lowFlags | (hold << TerminalOsConstants.DecryptionHoldFrameShift);
            if (hold < TerminalOsConstants.DecryptionRequiredHoldFrames)
                return;

            uint terminalHash = terminal.TerminalHash != 0u ? terminal.TerminalHash : puzzle.PuzzleID;
            uint nodeHash = terminal.NodeHash != 0u ? terminal.NodeHash : terminalHash;
            puzzle.Flags |= TerminalOsConstants.DecryptionFlagSolved;
            SignalBus<TerminalUnlockedSignal>.TryEnqueueBounded(UnlockedSignals, UnlockedSignalsBudget, new TerminalUnlockedSignal
            {
                PuzzleID = puzzle.PuzzleID,
                NodeHash = nodeHash,
                TerminalHash = terminalHash,
                Frame = Frame,
                AlignmentAccuracy01 = puzzle.AlignmentAccuracy01,
                Flags = puzzle.Flags
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeRange(float value, float min, float max, float fallback)
        {
            return math.isfinite(value) ? math.clamp(value, min, max) : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float WrapPhase(float value)
        {
            const float twoPi = math.PI * 2f;
            float safe = math.isfinite(value) ? value : 0f;
            return safe - math.floor(safe * math.rcp(twoPi)) * twoPi;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float WrapSignedPhase(float value)
        {
            const float twoPi = math.PI * 2f;
            float wrapped = value - math.floor((value + math.PI) * math.rcp(twoPi)) * twoPi;
            return wrapped - math.PI;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateTerminalButtonsJob : IJobParallelFor
    {
        [ReadOnly] [NoAlias] public NativeArray<TerminalInteractionDTO> Interactions;
        [ReadOnly] [NoAlias] public NativeArray<TerminalPlaneDTO> Planes;
        [ReadOnly] [NoAlias] public NativeArray<ButtonAABBDTO> Buttons;
        public int TerminalCount;
        public int ButtonCount;
        public uint Frame;
        public global::Hecton8.Core.MpscSignalRingBuffer<TerminalCommandSignal>.ParallelWriter Commands;
        [NativeDisableParallelForRestriction] public NativeArray<int> CommandsBudget;
        public global::Hecton8.Core.MpscSignalRingBuffer<InteractionUiSignal>.ParallelWriter UiSignals;
        [NativeDisableParallelForRestriction] public NativeArray<int> UiSignalsBudget;
        public void Execute(int index)
        {
            if ((uint)index >= (uint)TerminalCount)
                return;

            TerminalInteractionDTO interaction = Interactions[index];
            if ((interaction.InteractionFlags & TerminalOsConstants.InteractionFlagHover) == 0u)
                return;

            float2 uv = interaction.LocalHitUV;
            if (!math.all(math.isfinite(uv)))
                return;

            bool clicked = (interaction.InteractionFlags & TerminalOsConstants.InteractionFlagPress) != 0u;
            TerminalPlaneDTO plane = Planes[index];
            int safeButtonCount = math.max(0, ButtonCount);
            int firstButton = (int)math.min(plane.LayoutFirstButton, (uint)safeButtonCount);
            int availableButtons = math.max(0, safeButtonCount - firstButton);
            int localButtonCount = (int)math.min(plane.LayoutButtonCount, (uint)availableButtons);
            int buttonEnd = firstButton + localButtonCount;
            for (int i = firstButton; i < buttonEnd; i++)
            {
                ButtonAABBDTO button = Buttons[i];
                if (button.TerminalHash != interaction.TerminalHash ||
                    (button.Flags & TerminalOsConstants.ButtonFlagEnabled) == 0u)
                {
                    continue;
                }

                float4 rect = button.RectUv;
                bool inside = uv.x >= rect.x && uv.y >= rect.y && uv.x <= rect.z && uv.y <= rect.w;
                if (!inside)
                    continue;

                SignalBus<InteractionUiSignal>.TryEnqueueBounded(UiSignals, UiSignalsBudget, new InteractionUiSignal
                {
                    TargetAup = plane.CenterAup,
                    TargetHash = interaction.TerminalHash,
                    ToolHash = button.CommandHash,
                    State = TerminalOsConstants.InteractionUiStateShow,
                    Flags = TerminalOsConstants.InteractionUiFlagTerminal
                });

                if (clicked)
                {
                    SignalBus<TerminalCommandSignal>.TryEnqueueBounded(Commands, CommandsBudget, new TerminalCommandSignal
                    {
                        TerminalHash = interaction.TerminalHash,
                        CommandHash = button.CommandHash,
                        LocalUv = uv
                    });
                }

                return;
            }
        }
    }
}
