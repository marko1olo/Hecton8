using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
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
        public const int TerminalPlaneStrideBytes = 128;
        public const int GazeRayStrideBytes = 80;
        public const int ButtonAabbStrideBytes = 32;
        public const int FixedStringPayloadOffsetBytes = 2;
        public const int MaxFixedStringPayloadBytes = 30;
        public const int GlyphCount = 256;
        public const int BlackBoxFrameCount = 300;
        public const int MaxQueuedClicks = 64;
        public const int VirtualButtonCapacity = TerminalCapacity * 2;
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
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.TerminalStateStrideBytes)]
    public struct TerminalStateDTO
    {
        [FieldOffset(0)] public uint TerminalHash;
        [FieldOffset(4)] public uint BackgroundColor;
        [FieldOffset(7)] public byte IsDirty;
        [FieldOffset(8)] public float Value1;
        [FieldOffset(12)] public float Value2;
        [FieldOffset(16)] public FixedString32Bytes TextLine;
    }

    [StructLayout(LayoutKind.Sequential, Size = TerminalOsConstants.ScreenCommandStrideBytes)]
    public struct ScreenCommandDTO
    {
        public uint FontAtlasUV_Packed;
        public float2 Position;
        public float Scale;
    }

    [StructLayout(LayoutKind.Sequential, Size = 24)]
    public struct TerminalVirtualButtonDTO
    {
        public uint TerminalHash;
        public uint CommandHash;
        public float4 RectUv;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.ButtonAabbStrideBytes)]
    public struct ButtonAABBDTO
    {
        [FieldOffset(0)] public float4 RectUv;
        [FieldOffset(16)] public uint TerminalHash;
        [FieldOffset(20)] public uint CommandHash;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.TerminalInteractionStrideBytes)]
    public struct TerminalInteractionDTO
    {
        [FieldOffset(0)] public uint TerminalHash;
        [FieldOffset(4)] public float2 LocalHitUV;
        [FieldOffset(12)] public uint InteractionFlags;
        [FieldOffset(16)] public float Distance;
        [FieldOffset(20)] public byte _pad0;
        [FieldOffset(21)] public byte _pad1;
        [FieldOffset(22)] public byte _pad2;
        [FieldOffset(23)] public byte _pad3;
        [FieldOffset(24)] public byte _pad4;
        [FieldOffset(25)] public byte _pad5;
        [FieldOffset(26)] public byte _pad6;
        [FieldOffset(27)] public byte _pad7;
        [FieldOffset(28)] public byte _pad8;
        [FieldOffset(29)] public byte _pad9;
        [FieldOffset(30)] public byte _pad10;
        [FieldOffset(31)] public byte _pad11;
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
        [FieldOffset(116)] public uint _pad0;
        [FieldOffset(120)] public uint _pad1;
        [FieldOffset(124)] public uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = TerminalOsConstants.GazeRayStrideBytes)]
    public struct GazeRayDTO
    {
        [FieldOffset(0)] public AbsoluteUniversePosition OriginAup;
        [FieldOffset(48)] public float3 Direction;
        [FieldOffset(60)] public uint InteractionFlags;
        [FieldOffset(64)] public uint Frame;
        [FieldOffset(68)] public float2 ScrollDelta;
        [FieldOffset(76)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 8)]
    public partial struct MockPowerStateSignal
    {
        public uint Frame;
        public float MockPowerLevel;
    }

    [StructLayout(LayoutKind.Sequential, Size = 8)]
    public partial struct MockDamageScalarSignal
    {
        public uint Frame;
        public float Damage01;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public partial struct MockPowerStatusSignal
    {
        public uint Frame;
        public uint PoweredMask0;
        public uint PoweredMask1;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public partial struct TerminalClickSignal : Hecton8.Core.Contracts.Signals.ISignal
    {
        public uint TerminalHash;
        public float2 LocalUv;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public partial struct TerminalCommandSignal : Hecton8.Core.Contracts.Signals.ISignal
    {
        public uint TerminalHash;
        public uint CommandHash;
        public float2 LocalUv;
    }

    [StructLayout(LayoutKind.Sequential, Size = 80)]
    public struct TerminalPanelInstanceDTO
    {
        public float4x4 LocalToWorld;
        public float4 SliceFlags;
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

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct UpdateTerminalTextJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction] public TerminalStateDTO* States;
        [ReadOnly] public NativeArray<MockPowerStateSignal> PowerSignals;
        [ReadOnly] public NativeArray<MockDamageScalarSignal> DamageSignals;
        [ReadOnly] public NativeArray<MockPowerStatusSignal> PowerStatusSignals;
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

            if (!powered)
            {
                state.Value1 = 0f;
                state.Value2 = damage01;
                state.BackgroundColor = 0u;
                TerminalAsciiFormatter.WritePowerLine(ref state.TextLine, 0, damagePercent, false);
                if (previousPowered != 0 || previousPercent != 0 || previousDamagePercent != damagePercent)
                    state.IsDirty = 1;
                return;
            }

            uint background = damage01 > 0.5f ? 0x00101830u : 0x00061418u;
            state.Value1 = power01;
            state.Value2 = damage01;
            state.BackgroundColor = background;
            TerminalAsciiFormatter.WritePowerLine(ref state.TextLine, powerPercent, damagePercent, true);
            if (previousPowered == 0 || previousPercent != powerPercent || previousDamagePercent != damagePercent)
                state.IsDirty = 1;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct TerminalClickResolveJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<TerminalClickSignal>.ReadOnly Clicks;
        [ReadOnly] public NativeArray<ButtonAABBDTO> Buttons;
        public int ClickCount;
        public int ButtonCount;
        public NativeQueue<TerminalCommandSignal>.ParallelWriter Commands;

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

                Commands.Enqueue(new TerminalCommandSignal
                {
                    TerminalHash = click.TerminalHash,
                    CommandHash = button.CommandHash,
                    LocalUv = uv
                });
                return;
            }
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct MockGazeRayJob : IJob
    {
        public NativeArray<GazeRayDTO> GazeRays;
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

            float phase = (Frame & 1023u) * 0.006135923f;
            float sway = math.sin(phase) * math.max(0f, MicroSwayRadians);
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
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CullTerminalsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<TerminalPlaneDTO> Planes;
        [ReadOnly] public NativeArray<GazeRayDTO> GazeRays;
        public NativeArray<TerminalInteractionDTO> Interactions;
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

            float3 delta = (float3)AbsoluteUniversePosition.DeltaMetersClamped(in plane.CenterAup, in gaze.OriginAup);
            if (!math.all(math.isfinite(delta)) || !math.all(math.isfinite(gaze.Direction)))
            {
                result.InteractionFlags = TerminalOsConstants.InteractionFlagNonFinite;
                Interactions[index] = result;
                return;
            }

            float distanceSq = math.lengthsq(delta);
            float maxDistance = math.max(0.1f, MaxDistanceMeters);
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
            result.Distance = math.sqrt(math.max(0f, distanceSq));
            Interactions[index] = result;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct TerminalIntersectionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<TerminalPlaneDTO> Planes;
        [ReadOnly] public NativeArray<GazeRayDTO> GazeRays;
        public NativeArray<TerminalInteractionDTO> Interactions;
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
            float3 normal = math.normalizesafe(plane.Normal, new float3(0f, 0f, -1f));
            float3 right = math.normalizesafe(plane.Right, new float3(1f, 0f, 0f));
            float3 up = math.normalizesafe(plane.Up, new float3(0f, 1f, 0f));
            float3 direction = math.normalizesafe(gaze.Direction, new float3(0f, 0f, 1f));
            float3 centerFromOrigin = (float3)AbsoluteUniversePosition.DeltaMetersClamped(in plane.CenterAup, in gaze.OriginAup);
            float denom = math.dot(direction, normal);

            current.TerminalHash = plane.TerminalHash;
            current.LocalHitUV = default;
            current.Distance = float.MaxValue;
            current.InteractionFlags &= TerminalOsConstants.InteractionFlagCandidate;

            if (!math.all(math.isfinite(centerFromOrigin)) ||
                !math.all(math.isfinite(normal)) ||
                !math.all(math.isfinite(direction)) ||
                math.abs(denom) < 0.00001f)
            {
                current.InteractionFlags |= TerminalOsConstants.InteractionFlagNonFinite;
                Interactions[index] = current;
                return;
            }

            float distance = math.dot(centerFromOrigin, normal) / denom;
            float maxDistance = math.max(0.1f, MaxDistanceMeters);
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

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateTerminalButtonsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<TerminalInteractionDTO> Interactions;
        [ReadOnly] public NativeArray<TerminalPlaneDTO> Planes;
        [ReadOnly] public NativeArray<ButtonAABBDTO> Buttons;
        public int TerminalCount;
        public int ButtonCount;
        public uint Frame;
        public NativeQueue<TerminalCommandSignal>.ParallelWriter Commands;
        public NativeQueue<InteractionUiSignal>.ParallelWriter UiSignals;

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
            for (int i = 0; i < ButtonCount; i++)
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

                if (clicked)
                {
                    Commands.Enqueue(new TerminalCommandSignal
                    {
                        TerminalHash = interaction.TerminalHash,
                        CommandHash = button.CommandHash,
                        LocalUv = uv
                    });

                    TerminalPlaneDTO plane = Planes[index];
                    UiSignals.Enqueue(new InteractionUiSignal
                    {
                        TargetAup = plane.CenterAup,
                        TargetHash = interaction.TerminalHash,
                        ToolHash = button.CommandHash,
                        State = TerminalOsConstants.InteractionUiStateShow,
                        Flags = TerminalOsConstants.InteractionUiFlagTerminal
                    });
                }

                return;
            }
        }
    }
}
