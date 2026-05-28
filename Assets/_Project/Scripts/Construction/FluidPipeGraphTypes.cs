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

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
    public struct FluidPipeTelemetryEntry
    {
        [System.Runtime.InteropServices.FieldOffset(0)]
        public int FrameIndex;
        [System.Runtime.InteropServices.FieldOffset(4)]
        public int NodeCount;
        [System.Runtime.InteropServices.FieldOffset(8)]
        public int RuptureCount;
        [System.Runtime.InteropServices.FieldOffset(12)]
        public int NanCount;
        [System.Runtime.InteropServices.FieldOffset(16)]
        public float TotalWater;
        [System.Runtime.InteropServices.FieldOffset(20)]
        public float TotalOxygen;
        [System.Runtime.InteropServices.FieldOffset(24)]
        public float MaxPressureKPa;
        [System.Runtime.InteropServices.FieldOffset(28)]
        public uint StateHash;
        [System.Runtime.InteropServices.FieldOffset(32)]
        private byte _pad0;
        [System.Runtime.InteropServices.FieldOffset(33)]
        private byte _pad1;
        [System.Runtime.InteropServices.FieldOffset(34)]
        private byte _pad2;
        [System.Runtime.InteropServices.FieldOffset(35)]
        private byte _pad3;
        [System.Runtime.InteropServices.FieldOffset(36)]
        private byte _pad4;
        [System.Runtime.InteropServices.FieldOffset(37)]
        private byte _pad5;
        [System.Runtime.InteropServices.FieldOffset(38)]
        private byte _pad6;
        [System.Runtime.InteropServices.FieldOffset(39)]
        private byte _pad7;
        [System.Runtime.InteropServices.FieldOffset(40)]
        private byte _pad8;
        [System.Runtime.InteropServices.FieldOffset(41)]
        private byte _pad9;
        [System.Runtime.InteropServices.FieldOffset(42)]
        private byte _pad10;
        [System.Runtime.InteropServices.FieldOffset(43)]
        private byte _pad11;
        [System.Runtime.InteropServices.FieldOffset(44)]
        private byte _pad12;
        [System.Runtime.InteropServices.FieldOffset(45)]
        private byte _pad13;
        [System.Runtime.InteropServices.FieldOffset(46)]
        private byte _pad14;
        [System.Runtime.InteropServices.FieldOffset(47)]
        private byte _pad15;
        [System.Runtime.InteropServices.FieldOffset(48)]
        private byte _pad16;
        [System.Runtime.InteropServices.FieldOffset(49)]
        private byte _pad17;
        [System.Runtime.InteropServices.FieldOffset(50)]
        private byte _pad18;
        [System.Runtime.InteropServices.FieldOffset(51)]
        private byte _pad19;
        [System.Runtime.InteropServices.FieldOffset(52)]
        private byte _pad20;
        [System.Runtime.InteropServices.FieldOffset(53)]
        private byte _pad21;
        [System.Runtime.InteropServices.FieldOffset(54)]
        private byte _pad22;
        [System.Runtime.InteropServices.FieldOffset(55)]
        private byte _pad23;
        [System.Runtime.InteropServices.FieldOffset(56)]
        private byte _pad24;
        [System.Runtime.InteropServices.FieldOffset(57)]
        private byte _pad25;
        [System.Runtime.InteropServices.FieldOffset(58)]
        private byte _pad26;
        [System.Runtime.InteropServices.FieldOffset(59)]
        private byte _pad27;
        [System.Runtime.InteropServices.FieldOffset(60)]
        private byte _pad28;
        [System.Runtime.InteropServices.FieldOffset(61)]
        private byte _pad29;
        [System.Runtime.InteropServices.FieldOffset(62)]
        private byte _pad30;
        [System.Runtime.InteropServices.FieldOffset(63)]
        private byte _pad31;
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
            if (UnsafeUtility.SizeOf<FluidPipeRuptureRecord>() != 48 ||
                UnsafeUtility.SizeOf<FluidPipeTelemetryEntry>() != 64)
            {
                return false;
            }

#if UNITY_EDITOR
            return OffsetOf<FluidPipeRuptureRecord>(nameof(FluidPipeRuptureRecord.NodeIndex)) == 0 &&
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
                   OffsetOf<FluidPipeTelemetryEntry>(nameof(FluidPipeTelemetryEntry.FrameIndex)) == 0 &&
                   OffsetOf<FluidPipeTelemetryEntry>(nameof(FluidPipeTelemetryEntry.NodeCount)) == 4 &&
                   OffsetOf<FluidPipeTelemetryEntry>(nameof(FluidPipeTelemetryEntry.RuptureCount)) == 8 &&
                   OffsetOf<FluidPipeTelemetryEntry>(nameof(FluidPipeTelemetryEntry.NanCount)) == 12 &&
                   OffsetOf<FluidPipeTelemetryEntry>(nameof(FluidPipeTelemetryEntry.TotalWater)) == 16 &&
                   OffsetOf<FluidPipeTelemetryEntry>(nameof(FluidPipeTelemetryEntry.TotalOxygen)) == 20 &&
                   OffsetOf<FluidPipeTelemetryEntry>(nameof(FluidPipeTelemetryEntry.MaxPressureKPa)) == 24 &&
                   OffsetOf<FluidPipeTelemetryEntry>(nameof(FluidPipeTelemetryEntry.StateHash)) == 28;
#else
            return true;
#endif
        }

#if UNITY_EDITOR
        private static int OffsetOf<T>(string fieldName)
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }
#endif
    }
}
