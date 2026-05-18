using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
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
        public const int FixedStringPayloadOffsetBytes = 2;
        public const int MaxFixedStringPayloadBytes = 30;
        public const int GlyphCount = 256;
        public const int BlackBoxFrameCount = 300;
        public const int MaxQueuedClicks = 64;
        public const int VirtualButtonCapacity = TerminalCapacity * 2;
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

    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct TerminalTelemetryEntry
    {
        public int Frame;
        public int TerminalCount;
        public int DirtyCount;
        public int DispatchedCount;
        public float FormatMainThreadMilliseconds;
        public float UploadMicroseconds;
        public float DispatchMicroseconds;
        public uint FaultFlags;
        public uint LayoutHash;
        public uint Reserved0;
        public float LastPower01;
        public float LastDamage01;
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
        [ReadOnly] public NativeArray<TerminalVirtualButtonDTO> Buttons;
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
                TerminalVirtualButtonDTO button = Buttons[i];
                if (button.TerminalHash != click.TerminalHash)
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
}
