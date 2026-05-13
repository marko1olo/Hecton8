using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.AI.Cognition
{
    public static class AlphaLeviathanPhase
    {
        public const byte Hidden = 0;
        public const byte Circling = 1;
        public const byte FalseCharge = 2;
        public const byte Strike = 3;
        public const byte VeerOff = Strike;
    }

    public static class AlphaLeviathanTelemetryFlags
    {
        public const byte LowTierRadialFallback = 1 << 0;
        public const byte SdfDiveRequested = 1 << 1;
        public const byte PlayerGazeBreak = 1 << 2;
        public const byte RoarEmitted = 1 << 3;
        public const byte Fault = 1 << 4;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
    public struct AlphaLeviathanTelemetryEntry
    {
        public uint Frame;
        public ushort Slot;
        public byte Phase;
        public byte Flags;
        public float DistanceToPlayerMeters;
        public float FogRingDistanceMeters;
        public float3 Position;
        public float3 PlayerPosition;
        public float3 DesiredDirection;
        public uint StateHash;
        public uint Reserved0;
        public uint Reserved1;
    }
}
