using System;
using System.Runtime.InteropServices;
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

    [StructLayout(LayoutKind.Sequential)]
    public struct FluidPipeRuptureRecord
    {
        public int NodeIndex;
        public int NetworkId;
        public int RoomIndex;
        public int FrameIndex;
        public float PressureKPa;
        public float Contents;
        public float Flow01;
        public uint NodeHash;
        public byte ContentKind;
        public byte Flags;
        public ushort Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FluidPipeTelemetryEntry
    {
        public int FrameIndex;
        public int NodeCount;
        public int RuptureCount;
        public int NanCount;
        public float TotalWater;
        public float TotalOxygen;
        public float MaxPressureKPa;
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
        public const uint FnvOffset = 2166136261u;
        public const uint FnvPrime = 16777619u;

        public static float ResolveCadenceSeconds(FluidPipeMathLod lod)
        {
            switch (lod)
            {
                case FluidPipeMathLod.Ultra:
                    return UltraCadenceSeconds;
                case FluidPipeMathLod.High:
                    return HighCadenceSeconds;
                case FluidPipeMathLod.Middle:
                    return MiddleCadenceSeconds;
                default:
                    return LowCadenceSeconds;
            }
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
}
