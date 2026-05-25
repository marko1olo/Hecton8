using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Logistics
{
    [Flags]
    public enum FluidPipeFlags : byte
    {
        None = 0,
        Active = 1 << 0,
        Ruptured = 1 << 1,
        Outside = 1 << 2,
        PumpIngress = 1 << 3,
        OxygenSource = 1 << 4,
        RoomCoupled = 1 << 5,
        Disabled = 1 << 7
    }

    public enum FluidPipeContentKind : byte
    {
        Empty = 0,
        Water = 1,
        Oxygen = 2
    }

    public enum FluidPipeMathLod : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct FluidPipeRuptureRecord
    {
        [FieldOffset(0)]
        public int NodeIndex;
        [FieldOffset(4)]
        public int NetworkId;
        [FieldOffset(8)]
        public int RoomIndex;
        [FieldOffset(12)]
        public int FrameIndex;
        [FieldOffset(16)]
        public float PressureKPa;
        [FieldOffset(20)]
        public float Contents;
        [FieldOffset(24)]
        public float Flow01;
        [FieldOffset(28)]
        public uint NodeHash;
        [FieldOffset(32)]
        public byte ContentKind;
        [FieldOffset(33)]
        public byte Flags;
        [FieldOffset(34)]
        public ushort Reserved;
        [FieldOffset(36)]
        private byte _pad0;
        [FieldOffset(37)]
        private byte _pad1;
        [FieldOffset(38)]
        private byte _pad2;
        [FieldOffset(39)]
        private byte _pad3;
        [FieldOffset(40)]
        private byte _pad4;
        [FieldOffset(41)]
        private byte _pad5;
        [FieldOffset(42)]
        private byte _pad6;
        [FieldOffset(43)]
        private byte _pad7;
        [FieldOffset(44)]
        private byte _pad8;
        [FieldOffset(45)]
        private byte _pad9;
        [FieldOffset(46)]
        private byte _pad10;
        [FieldOffset(47)]
        private byte _pad11;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FluidPipeTelemetryEntry
    {
        [FieldOffset(0)]
        public int FrameIndex;
        [FieldOffset(4)]
        public int NodeCount;
        [FieldOffset(8)]
        public int RuptureCount;
        [FieldOffset(12)]
        public int NanCount;
        [FieldOffset(16)]
        public float TotalWater;
        [FieldOffset(20)]
        public float TotalOxygen;
        [FieldOffset(24)]
        public float MaxPressureKPa;
        [FieldOffset(28)]
        public uint StateHash;
    }

    public static class FluidPipeGraphConstants
    {
        public const int BlackBoxFrameCount = 300;
        public const float MinCapacity = 0.001f;
        public const float MinMaxPressureKPa = 0.1f;
        public const float DefaultFlowRate = 0.08f;
        public const float LowCadenceSeconds = 1f;
        public const float MiddleCadenceSeconds = 0.25f;
        public const float HighCadenceSeconds = 0.1f;
        public const float UltraCadenceSeconds = 0.1f;
        public const float AuthoritativeCadenceSeconds = 0.1f;
        public const uint FnvOffset = 2166136261u;
        public const uint FnvPrime = 16777619u;

        public static float ResolveCadenceSeconds(FluidPipeMathLod lod)
        {
            return AuthoritativeCadenceSeconds;
        }

        public static uint MixHash(uint hash, uint value)
        {
            return (hash ^ value) * FnvPrime;
        }

        public static uint HashNode(int nodeIndex, int networkId, byte contentKind)
        {
            uint hash = FnvOffset;
            hash = MixHash(hash, (uint)nodeIndex);
            hash = MixHash(hash, (uint)networkId);
            hash = MixHash(hash, contentKind);
            return hash;
        }

        public static float SanitizeFiniteNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }
    }

    internal static class FluidPipeGraphLayoutSentinel
    {
        internal static bool ValidateRuntimeDtos()
        {
            return UnsafeUtility.SizeOf<FluidPipeRuptureRecord>() == 48 &&
                   OffsetOf<FluidPipeRuptureRecord>(nameof(FluidPipeRuptureRecord.NodeIndex)) == 0 &&
                   OffsetOf<FluidPipeRuptureRecord>(nameof(FluidPipeRuptureRecord.NetworkId)) == 4 &&
                   OffsetOf<FluidPipeRuptureRecord>(nameof(FluidPipeRuptureRecord.RoomIndex)) == 8 &&
                   OffsetOf<FluidPipeRuptureRecord>(nameof(FluidPipeRuptureRecord.FrameIndex)) == 12 &&
                   OffsetOf<FluidPipeRuptureRecord>(nameof(FluidPipeRuptureRecord.PressureKPa)) == 16 &&
                   OffsetOf<FluidPipeRuptureRecord>(nameof(FluidPipeRuptureRecord.Contents)) == 20 &&
                   OffsetOf<FluidPipeRuptureRecord>(nameof(FluidPipeRuptureRecord.Flow01)) == 24 &&
                   OffsetOf<FluidPipeRuptureRecord>(nameof(FluidPipeRuptureRecord.NodeHash)) == 28 &&
                   OffsetOf<FluidPipeRuptureRecord>(nameof(FluidPipeRuptureRecord.ContentKind)) == 32 &&
                   OffsetOf<FluidPipeRuptureRecord>(nameof(FluidPipeRuptureRecord.Flags)) == 33 &&
                   OffsetOf<FluidPipeRuptureRecord>(nameof(FluidPipeRuptureRecord.Reserved)) == 34 &&
                   OffsetOf<FluidPipeRuptureRecord>("_pad0") == 36 &&
                   OffsetOf<FluidPipeRuptureRecord>("_pad11") == 47 &&
                   UnsafeUtility.SizeOf<FluidPipeTelemetryEntry>() == 32 &&
                   OffsetOf<FluidPipeTelemetryEntry>(nameof(FluidPipeTelemetryEntry.FrameIndex)) == 0 &&
                   OffsetOf<FluidPipeTelemetryEntry>(nameof(FluidPipeTelemetryEntry.NodeCount)) == 4 &&
                   OffsetOf<FluidPipeTelemetryEntry>(nameof(FluidPipeTelemetryEntry.RuptureCount)) == 8 &&
                   OffsetOf<FluidPipeTelemetryEntry>(nameof(FluidPipeTelemetryEntry.NanCount)) == 12 &&
                   OffsetOf<FluidPipeTelemetryEntry>(nameof(FluidPipeTelemetryEntry.TotalWater)) == 16 &&
                   OffsetOf<FluidPipeTelemetryEntry>(nameof(FluidPipeTelemetryEntry.TotalOxygen)) == 20 &&
                   OffsetOf<FluidPipeTelemetryEntry>(nameof(FluidPipeTelemetryEntry.MaxPressureKPa)) == 24 &&
                   OffsetOf<FluidPipeTelemetryEntry>(nameof(FluidPipeTelemetryEntry.StateHash)) == 28;
        }

        private static int OffsetOf<T>(string fieldName)
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }
    }
}
